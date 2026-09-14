# Fusion — Chat-Anwendung

Semesterprojekt (SAE Institute Vienna, Modul 2). Eine Desktop-Chat-Anwendung aus zwei
eigenständigen Teilen:

| Teil       | Technologie                         | Ordner    |
|------------|-------------------------------------|-----------|
| **Client** | WPF, .NET Framework 4.8, MVVM       | `Client/` |
| **Server** | ASP.NET Core 8 REST-API, EF Core, PostgreSQL | `Server/` |

Der Client spricht ausschließlich über HTTP/JSON mit dem Server. Authentifizierung
läuft über JWT-Bearer-Tokens; Passwörter werden serverseitig mit PBKDF2-HMAC-SHA256
(210.000 Iterationen) gehasht.

**Funktionsumfang:** Registrierung, Login/Logout, Direktnachrichten, Gruppenchats,
Live-Aktualisierung per Polling, Konversationsliste mit Vorschau der letzten Nachricht.

Offene Punkte und geplante Ausbaustufen: [TODO.md](TODO.md).
Ergebnisse des Code-Reviews: [docs/FINDINGS.md](docs/FINDINGS.md).

---

## Schnellstart

### Voraussetzungen

* .NET SDK 8.0 oder neuer (`dotnet --version`)
* Visual Studio 2022 mit „.NET-Desktopentwicklung" (der WPF-Client braucht MSBuild)
* Docker (für PostgreSQL) **oder** eine lokal installierte PostgreSQL-Instanz

### 1. Datenbank starten

```bash
docker compose up -d
```

Legt einen Container `fusion-postgres` (PostgreSQL 16) mit Datenbank, Benutzer und
persistentem Volume an. Bereitschaft prüfen:

```bash
docker compose ps
```

### 2. Zugangsdaten hinterlegen

Verbindungszeichenfolge und JWT-Signaturschlüssel stehen **nicht** im Repository.
Für die Entwicklung liegen sie in den User Secrets:

```bash
cd Server
dotnet user-secrets set "ConnectionStrings:PostgreSQL" "Host=localhost;Port=5432;Database=fusion;Username=fusion;Password=fusionpw"
dotnet user-secrets set "Jwt:Key" "<mindestens 32 Zeichen Zufallsschlüssel>"
```

Einen Schlüssel erzeugen:

```powershell
$b = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
[Convert]::ToBase64String($b)
```

Im Betrieb stattdessen Umgebungsvariablen setzen: `ConnectionStrings__PostgreSQL`
und `Jwt__Key`.

### 3. Server starten

```bash
cd Server
dotnet run --urls "http://127.0.0.1:5214"
```

Der Server wendet ausstehende EF-Migrationen beim Start selbst an — eine leere
Datenbank genügt. Beim Hochfahren protokolliert er, mit welcher Datenbank er sich
verbindet (ohne Passwort):

```
[config] Database: Host=localhost;Port=5432;Database=fusion;Username=fusion
[config] Applying 4 pending migration(s): …
[config] Database schema is up to date.
```

Prüfen: <http://127.0.0.1:5214/health> muss `"Healthy"` liefern.
Swagger-UI (nur in Development): <http://127.0.0.1:5214/swagger>

> ⚠️ **Stolperfalle:** Eine Umgebungsvariable `ConnectionStrings__PostgreSQL`
> überschreibt sowohl `appsettings.json` als auch die User Secrets. Zeigt sie auf
> einen falschen Host, bricht der Server beim Start mit einer entsprechenden Meldung
> ab. Prüfen mit:
> ```powershell
> [Environment]::GetEnvironmentVariable('ConnectionStrings__PostgreSQL','User')
> ```

### 4. Client starten

Die Server-Adresse steht in [`Client/App.config`](Client/App.config) und ist ohne
Neukompilieren änderbar:

```xml
<add key="ApiBaseUrl" value="http://127.0.0.1:5214/"/>
```

Bauen und starten:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
    Client\WPF_Fusion.csproj /t:Restore,Build
.\Client\bin\Debug\WPF_Fusion.exe
```

Oder die Solution `Client/WPF_Fusion.sln` in Visual Studio öffnen — sie enthält
Client **und** Server.

### 5. Ausprobieren

1. **Register** klicken, E-Mail und Passwort (min. 8 Zeichen) eingeben, im Dialog
   einen Benutzernamen wählen.
2. Denselben Vorgang für einen zweiten Benutzer in einer zweiten Client-Instanz.
3. Einloggen, links auf **Chats** wechseln.
4. Benutzernamen des Gegenübers ins Feld rechts neben **Add** eintragen, **Add**
   klicken — die Konversation entsteht.
5. Nachricht eintippen, **Enter** oder den Senden-Pfeil.
6. Für Gruppen: **Group** klicken, Name und mindestens zwei weitere Benutzernamen
   durch Komma getrennt eingeben.

---

## Konfiguration

| Schlüssel | Bedeutung | Vorgabe |
|-----------|-----------|---------|
| `ConnectionStrings:PostgreSQL` | Datenbankverbindung | — (Pflicht) |
| `Jwt:Issuer` | Aussteller im Token | `Server` |
| `Jwt:Audience` | Zielgruppe im Token | `Client` |
| `Jwt:Key` | HMAC-Signaturschlüssel, min. 32 Zeichen | — (Pflicht) |
| `Jwt:ExpiresMinutes` | Token-Gültigkeit | `120` |
| `Database:AutoMigrate` | Migrationen beim Start anwenden | außerhalb Production `true` |

Reihenfolge der Konfigurationsquellen (später schlägt früher):
`appsettings.json` → `appsettings.{Environment}.json` → User Secrets → Umgebungsvariablen.

Client-seitig, in `App.config`:

| Schlüssel | Bedeutung | Vorgabe |
|-----------|-----------|---------|
| `ApiBaseUrl` | Basisadresse der REST-API | `http://127.0.0.1:5214/` |

---

## Sicherheitshinweise

* **HTTP ist unverschlüsselt.** Für den Betrieb über ein Netzwerk gehört ein
  Reverse Proxy mit TLS davor, sonst gehen Passwörter und Tokens im Klartext über
  die Leitung. Siehe [TODO.md](TODO.md) 2.6.
* **Zugangsdaten gehören nicht ins Repository.** `appsettings.json` enthält nur
  Platzhalter; `.gitignore` schließt `appsettings-*.json` aus.
* Der Zugriff auf Konversationen ist auf deren Teilnehmer beschränkt; die
  Anmeldeendpunkte sind auf 10 Anfragen pro Minute und IP begrenzt.

---

## Dokumentation

* [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — Aufbau, Datenfluss, API-Referenz
* [docs/FINDINGS.md](docs/FINDINGS.md) — Befunde des Code-Reviews, nach Schweregrad
* [TODO.md](TODO.md) — priorisierte Arbeitsliste

---

## Projektstruktur

```
WPF_Fusion/
├─ Client/                    WPF-Anwendung (.NET Framework 4.8)
│  ├─ Infrastructure/         RelayCommand, Session, DialogService, ApiException, PasswordBoxHelper
│  ├─ Interface/              IAuthService, IChatService, IUserService + DTOs
│  ├─ MVVM/
│  │  ├─ Model/               ContactModel, MessageModel, LoginResult
│  │  ├─ View/                ChatView, HomeView, UsernameView, CreateGroupView
│  │  └─ ViewModel/           Login-, Main-, Chat-, Username-, CreateGroup-ViewModel
│  ├─ Service/                HTTP-Clients gegen die REST-API
│  └─ Theme/                  XAML-ResourceDictionaries
├─ Server/                    ASP.NET Core 8 REST-API
│  ├─ Controllers/            Auth, Users, Conversations
│  ├─ Data/                   AppDbContext + JSON-Ablage für Chats
│  ├─ Infrastructure/         JwtTokenService, PasswordHasher
│  ├─ Migrations/             EF-Core-Migrationen
│  ├─ Models/                 EF-Entities
│  ├─ Persistence/            Records der JSON-Ablage
│  └─ Service/                DatabaseUserStore, JsonChatStore
├─ docs/                      Projektdokumentation
└─ docker-compose.yml         PostgreSQL für die Entwicklung
```
