# Architektur

## Überblick

```
┌──────────────────────────────┐          ┌────────────────────────────────┐
│  Client (WPF, .NET FW 4.8)   │          │  Server (ASP.NET Core 8)       │
│                              │          │                                │
│  LoginWindow ─┐              │          │  AuthController      /auth/*   │
│  MainWindow ──┼─ ViewModels  │  HTTP    │  UsersController     /users/*  │
│               │      │       │  JSON    │  ConversationsCtrl   /convers. │
│               │      ▼       │ ───────► │         │                      │
│         AuthApiService       │  Bearer  │         ├─ IUserStore ──► EF ──┼──► PostgreSQL
│         UserApiService       │   JWT    │         │                      │
│         ChatApiService       │          │         └─ IChatStore ─► JSON ─┼──► Data/*.json
│               │              │          │                                │
│         Session (static)     │          │  JwtTokenService               │
└──────────────────────────────┘          │  PasswordHasher (PBKDF2)       │
                                          └────────────────────────────────┘
```

**Zentrale Eigenheit:** Der Server hat *zwei* Persistenzschichten nebeneinander.
Benutzer liegen in PostgreSQL (EF Core), Konversationen und Nachrichten dagegen in
flachen JSON-Dateien (`Server/Data/conversations.json`, `messages.json`) — obwohl
EF-Migrationen für `Conversations`, `ConversationMembers` und `Message` existieren
und angewendet sind. Diese Tabellen sind zur Laufzeit ungenutzt. Siehe
[FINDINGS.md](FINDINGS.md) → A1.

---

## Ablauf: Registrierung und Login

```
Benutzer ─► LoginViewModel.Register()
              └─► DialogService.ShowDialog(UsernameViewModel)   Username abfragen
                    └─► AuthApiService.RegisterAsync()
                          └─► POST /auth/register {email, username, password}
                                └─► PasswordHasher.HashPassword()  PBKDF2, 210k Iter.
                                      └─► INSERT INTO "Users"

Benutzer ─► LoginViewModel.Login(PasswordBox)
              └─► AuthApiService.LoginAsync()
                    └─► POST /auth/login {email, password}
                          └─► PasswordHasher.VerifyPassword()  konstante Laufzeit
                                └─► JwtTokenService.CreateAccessToken()
                    ◄── {success, userId, accessToken, username}
                    ├─► Session.AccessToken / UserId / Username
                    ├─► HttpClient.DefaultRequestHeaders.Authorization = Bearer …
                    └─► GET /users/me   (autoritative Identität)
              └─► LoginSucceeded ─► LoginWindow.DialogResult = true
                    └─► App.OnStartup öffnet MainWindow
```

Das JWT enthält die Claims `sub` (UserId), `email` und `jti`. Serverseitig wird die
UserId über `ClaimTypes.NameIdentifier` / `sub` / `userId` / `UserId` aufgelöst
(`GetUserIdFromClaims`).

## Ablauf: Nachricht senden und empfangen

```
ChatView.Loaded ─► ChatViewModel.LoadConversationsAsync()
                     └─► GET /conversations           ─► Contacts befüllen

SelectedContact gesetzt ─► SwitchConversationAsync()
                             ├─► GET /conversations/{id}/messages?take=50&skip=0
                             ├─► _lastSeenUtc = Zeit der letzten Nachricht
                             └─► StartListening()   Polling-Schleife, 1,2 s

SendCommand ─► SendMessageAsync()
                 └─► POST /conversations/{id}/messages {content}
                       ├─► Controller prüft Teilnehmerschaft
                       └─► JsonChatStore.AddMessageAsync()
                             ├─► erzwingt streng monotonen Zeitstempel
                             └─► schreibt messages.json atomar (Temp-Datei + Replace)
                 ◄── MessageDto ─► lokal an SelectedContact.Messages angehängt

ListenLoopAsync (alle 1,2 s)
  └─► GET /conversations/{id}/messages?afterUtc=_lastSeenUtc
        ├─► nur Neues; Deduplizierung über MessageId als zweite Absicherung
        └─► MessagesAppended ─► ChatView scrollt ans Ende

Fehlerfälle
  401 ─► Session.NotifyExpired() ─► MainViewModel ─► zurück zum Login-Fenster
  Netzwerkausfall ─► ApiException ─► Hinweisbanner in der Chat-Ansicht
```

---

## API-Referenz

Basis-URL: `http://<host>:5214`. Alle Antworten sind JSON.
`[A]` = erfordert `Authorization: Bearer <token>`.

| Methode | Pfad | Auth | Beschreibung |
|---------|------|------|--------------|
| GET  | `/` | — | Liveness, liefert `"OK"` |
| GET  | `/health` | — | Liefert `"Healthy"` |
| POST | `/auth/register` | — | `{email, username, password}` → `200` / `400` / `409` |
| POST | `/auth/login` | — | `{email, password}` → `200` mit `accessToken` / `401` |
| GET  | `/users/me` | [A] | `{userId, username, email}` des Aufrufers |
| GET  | `/users/by-username/{name}` | [A] | `{userId}` oder `404` |
| GET  | `/users/exists/{name}` | [A] | `{exists: bool}` |
| GET  | `/conversations` | [A] | Konversationen des Aufrufers inkl. Teilnehmern |
| POST | `/conversations/direct` | [A] | `{otherUserId}` → Direktchat, idempotent |
| POST | `/conversations/group` | [A] | `{title, participantUserIds[]}` → Gruppe; Ersteller wird ergänzt, mindestens drei Mitglieder |
| GET  | `/conversations/{id}/messages` | [A] | Query: `take` (max. 500), `skip`, `afterUtc` |
| POST | `/conversations/{id}/messages` | [A] | `{content}` → erzeugte `MessageDto` |

### Zugriffsregeln

* Alle `/conversations`- und `/users`-Endpunkte erfordern ein gültiges JWT.
* Lesen und Schreiben einer Konversation ist auf deren Teilnehmer beschränkt.
  Nichtteilnehmer erhalten **`404`, nicht `403`** — so verrät der Endpunkt nicht,
  ob eine Konversations-ID existiert.
* `/auth/register` und `/auth/login` sind auf 10 Anfragen pro Minute begrenzt,
  getrennt je Client-IP und Endpunkt. Bei Überschreitung: `429` mit `Retry-After`.

### Nachrichtenabruf: zwei Betriebsarten

| Aufruf | Verhalten |
|--------|-----------|
| `?take=50&skip=0` | die **neuesten** 50 Nachrichten, chronologisch sortiert. `skip` blättert weiter in die Vergangenheit. |
| `?afterUtc=<ISO-8601>` | nur Nachrichten **nach** diesem Zeitpunkt, älteste zuerst, maximal 200. Grundlage des inkrementellen Pollings. |

Zeitstempel sind je Konversation streng monoton steigend. Ohne diese Garantie könnten
zwei in derselben Millisekunde gesendete Nachrichten denselben Wert tragen — die zweite
würde von `SentAtUtc > cursor` dauerhaft übersprungen.

---

## Datenmodell

### PostgreSQL (EF Core, aktiv genutzt)

```
Users
  Id             uuid  PK
  Email          text  UNIQUE
  Username       text  UNIQUE
  PasswordHash   text          PBKDF2$<iter>$<salt-b64>$<hash-b64>
  CreatedAtUtc   timestamptz
  LastLoginAtUtc timestamptz NULL
```

### PostgreSQL (migriert, zur Laufzeit ungenutzt)

`Clients`, `Conversations`, `ConversationMembers`, `Message`

### JSON-Dateien (aktiv genutzt für Chat)

```
Server/Data/conversations.json    [{ ConversationId, IsGroup, Title, ParticipantUserIds[] }]
Server/Data/messages.json         [{ MessageId, ConversationId, SenderUserId, Content, SentAtUtc }]
```

Zugriff serialisiert über ein `SemaphoreSlim` innerhalb **eines** Prozesses.
Jeder Schreibvorgang lädt und schreibt die gesamte Datei neu.

---

## Client-Aufbau (MVVM)

| Schicht | Typen | Aufgabe |
|---------|-------|---------|
| View | `LoginWindow`, `MainWindow`, `ChatView`, `HomeView`, `UsernameView`, `CreateGroupView` | XAML, nur Darstellung und UI-Events |
| ViewModel | `LoginViewModel`, `MainViewModel`, `ChatViewModel`, `UsernameViewModel`, `CreateGroupViewModel` | Zustand, Commands, Validierung |
| Model | `ContactModel`, `MessageModel`, `LoginResult` | UI-nahe Datenhaltung |
| Service | `AuthApiService`, `UserApiService`, `ChatApiService` | HTTP gegen die REST-API |
| Infrastructure | `RelayCommand`, `AsyncRelayCommand`, `ObservableObject`, `Session`, `DialogService`, `ApiException`, `PasswordBoxHelper` | Querschnitt |

View↔ViewModel-Zuordnung über implizite `DataTemplate`s in `App.xaml`.
Dialoge über `IDialogService` + `IDialogRequestClose` (ViewModel-getriebenes Schließen).
`PasswordBoxHelper` macht `PasswordBox.Password` bindbar (in WPF nicht von Haus aus möglich).

### Fenster-Lebenszyklus

`ShutdownMode` ist `OnExplicitShutdown`, damit das Schließen des Hauptfensters beim
Abmelden nicht den Prozess beendet:

```
App.OnStartup ─► ShowLogin()
                   ├─ Abbruch  ─► ExitApplication()
                   └─ Erfolg   ─► ShowMain()
                                    ├─ SignOutRequested ─► MainWindow schließen ─► ShowLogin()
                                    └─ Fenster geschlossen ─► ExitApplication()
```

### Bekannte Schwäche

**Abhängigkeiten werden nicht injiziert**, sondern über statische Felder auf `App`
(`App.AuthService`, `App.ChatService`, `App.UserApiService`) und den globalen statischen
`Session`-Zustand bezogen. Dadurch ist kein ViewModel isoliert testbar —
siehe [FINDINGS.md](FINDINGS.md) A5 und [../TODO.md](../TODO.md) 4.7.
