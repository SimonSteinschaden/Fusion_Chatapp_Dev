# Befunde des Code-Reviews

Stand der Erhebung: 14.09.2026. Alle mit **[verifiziert]** markierten Punkte wurden
gegen einen laufenden Server (lokales PostgreSQL 16, `dotnet run`) reproduziert, nicht
nur aus dem Code abgeleitet.

> **Bearbeitungsstand:** Die Befunde der Gruppen **B**, **S** und **F** sind behoben und
> durch Tests abgesichert (siehe [../TODO.md](../TODO.md), Stufen 1–3). Die Gruppen
> **A** und **D** sind bis auf die unten vermerkten Ausnahmen offen.
>
> Dieses Dokument bleibt absichtlich vollständig — es beschreibt den Ausgangszustand
> und dient als Nachweis, was warum geändert wurde.

Schweregrade: **B** Blocker · **S** Sicherheit · **F** Funktion · **A** Architektur · **D** Build/DevOps

Status: ✅ behoben · 🟡 teilweise · ⬜ offen

---

## B — Blocker: verhinderten den Betrieb

### B1 ✅ Umgebungsvariable überschreibt die Datenbank-Konfiguration [verifiziert]
Auf dem Entwicklungsrechner ist eine **benutzerweite** Umgebungsvariable gesetzt:

```
ConnectionStrings__PostgreSQL = Host=srv1266377.hstgr.cloud;Port=5432;Database=chatserverSae;Username=postgres;Password=Sae
```

Umgebungsvariablen haben in ASP.NET Core Vorrang vor `appsettings.json`. Der Server
verbindet sich deshalb *immer* zur Hostinger-Instanz, deren Zugangsdaten nicht mehr
gelten. Ergebnis: **jeder** datenbankgestützte Endpunkt antwortet mit HTTP 500
(`28P01: password authentication failed for user "postgres"`). Ohne Kenntnis dieser
Variable ist der Fehler praktisch nicht auffindbar — `appsettings.json` sieht korrekt aus.

**Behoben:** `Program.cs` protokolliert beim Start die verwendete Verbindung (ohne
Passwort) und bricht mit einer Meldung ab, die ausdrücklich auf die Umgebungsvariable
hinweist. Die Variable selbst liegt außerhalb des Repositorys und muss auf dem
Entwicklungsrechner entfernt werden.

### B2 ✅ Server-Adresse im Client hart verdrahtet und nicht erreichbar [verifiziert]
`Client/App.xaml.cs` setzte `BaseAddress = new Uri("http://82.29.177.130:5214/")`.
Dieser Host antwortet nicht (Verbindungs-Timeout). Der Client startete zwar, aber jeder
Login lief ins Leere; die Adresse war nur durch Neukompilieren änderbar.

**Behoben:** Adresse kommt aus `App.config` (`ApiBaseUrl`), mit Rückfall auf
`http://127.0.0.1:5214/`.

### B3 ✅ Client ruft den falschen Pfad für die eigene Identität [verifiziert]
`UserApiService.GetMeAsync()` rief `GET me`, der Server stellt aber `GET /users/me`
bereit → **404**. Die Methode gab still `null` zurück, die autoritative
Identitätsabfrage nach dem Login fiel wirkungslos aus. Es fiel nicht auf, weil die
Login-Antwort `username` und `userId` mitliefert.

**Behoben:** Pfad korrigiert, durch Test abgedeckt.

### B4 ✅ Kein Schema-Setup beim Start
`Program.cs` rief weder `Database.Migrate()` noch `EnsureCreated()`. Ein frisch
geklontes Repo startete gegen eine leere Datenbank und lieferte 500er, bis jemand
manuell `dotnet ef database update` ausführte.

**Behoben:** Migrationen werden beim Start angewendet, steuerbar über
`Database:AutoMigrate`. Gegen eine leere Datenbank verifiziert.

---

## S — Sicherheit

### S1 ✅ Jeder angemeldete Benutzer konnte jede Konversation mitlesen [verifiziert]
`ConversationsController.GetMessages` prüfte nur, dass *irgendein* gültiges Token
vorliegt — nicht, ob der Aufrufer Teilnehmer der angefragten Konversation ist.

Reproduktion: Nutzer A und B führen einen Direktchat. Ein unbeteiligter Nutzer C ruft
`GET /conversations/{id}/messages` mit seinem eigenen Token und erhält **HTTP 200 mit
dem vollständigen Nachrichtenverlauf**. Da Konversations-IDs GUIDs sind, war das nicht
trivial ausnutzbar, aber jede geleakte ID (Logs, Screenshot, Fehlermeldung) öffnete den
gesamten Verlauf.

**Behoben:** `EnsureParticipantAsync` schützt Lesen **und** Schreiben. Nichtteilnehmer
erhalten bewusst `404` statt `403`, damit der Endpunkt nicht verrät, ob eine
Konversations-ID existiert. Durch Tests für Direkt- und Gruppenchats abgedeckt.

### S2 ✅ Signaturschlüssel und Datenbankpasswörter im Klartext im Repository
* `Server/appsettings.json` → `Jwt:Key` im Klartext. Wer den Schlüssel kennt, kann
  beliebige gültige Tokens für beliebige Benutzer signieren — vollständige
  Authentifizierungsumgehung.
* `Server/appsettings-SimonPC.json` → `Username=postgres;Password=Sae` für einen
  öffentlich erreichbaren Host.
* Dieselben Werte liegen zusätzlich in `Server/out/`, `Server/out/out/`,
  `Server/out/out/out/` und in sechs ZIP-Archiven im Projektwurzelverzeichnis.

**Behoben:** Neuer 384-Bit-Signaturschlüssel erzeugt; Schlüssel und
Verbindungszeichenfolge liegen in User Secrets (Entwicklung) bzw. Umgebungsvariablen
(Betrieb). `appsettings.json` enthält nur Platzhalter, `appsettings-SimonPC.json` wurde
gelöscht. Die verbliebenen Kopien liegen ausschließlich in `bin/`, `out/` und den
ZIP-Archiven und sind über `.gitignore` von der Versionierung ausgeschlossen.

> **Noch offen, außerhalb des Codes:** Das Passwort des PostgreSQL-Kontos auf
> `srv1266377.hstgr.cloud` muss am Server selbst geändert werden.

### S2b ✅ SQL-Dump mit echten Nutzerdaten im Projektverzeichnis
*Beim Vorbereiten des ersten Commits gefunden, nicht Teil der ursprünglichen Erhebung.*
`chatserverSae.sql` im Projektwurzelverzeichnis ist ein vollständiger Datenbank-Dump
mit acht realen Benutzerdatensätzen — echte E-Mail-Adressen (u. a. private
Gmail-Adresse des Autors, Adressen von Mitstudierenden) plus zugehörige
PBKDF2-Passwort-Hashes. Wäre der Dump mitversioniert worden, hätten diese Daten mit
dem ersten Push dauerhaft öffentlich in der Git-Historie gestanden.

**Behoben:** `*.sql` in `.gitignore` aufgenommen, Datei aus dem Staging entfernt
(`git rm --cached`), Begründung im README dokumentiert. Das Schema, das der Dump
transportiert, steht vollständig und ohne Nutzerdaten in `Server/Migrations/` — ein
Dump zum Wiederherstellen des Schemas ist nicht nötig.

### S3 🟡 Unverschlüsselter Transport
Client und Server kommunizieren über HTTP. E-Mail, Passwort und JWT gehen im Klartext
über das Netz. `UseHttpsRedirection()` war zudem nur im Development-Environment aktiv —
also genau dort, wo es am wenigsten nötig ist.

**Teilweise behoben:** HSTS und HTTPS-Weiterleitung sind jetzt außerhalb von
Development aktiv. Ein echter Betrieb braucht zusätzlich einen Reverse Proxy mit
gültigem Zertifikat — siehe TODO 2.6.

### S4 ✅ Swagger und Stacktraces im Produktivbetrieb
`if (app.Environment.IsDevelopment() || app.Environment.IsProduction())` — diese
Bedingung ist faktisch immer wahr. Swagger-UI war öffentlich, und die Developer
Exception Page lieferte vollständige Stacktraces inklusive Verbindungszeichenfolge an
den Client. Im Test war der Hostname der Datenbank samt Benutzername in der
500er-Antwort lesbar.

**Behoben:** Swagger und Developer Exception Page nur noch in Development; sonst
`UseExceptionHandler` mit generischer `ProblemDetails`-Antwort.

### S5 ✅ Kein Brute-Force-Schutz
`/auth/login` kannte weder Rate-Limiting noch Account-Lockout noch Verzögerung nach
Fehlversuchen. Das starke Passwort-Hashing schützt die gespeicherten Hashes, nicht aber
gegen Online-Rateangriffe.

**Behoben:** 10 Anfragen pro Minute, getrennt je Client-IP **und** Endpunkt, mit
`Retry-After`-Header. Die Trennung nach Endpunkt verhindert, dass eine Serie von
Registrierungen den Login blockiert. Eine Kontosperre fehlt weiterhin — siehe TODO 5.7.

### S6 ✅ Debug-Ausgaben mit Sitzungsdaten
`AuthApiService` zeigte bei **jedem** Login eine `MessageBox` mit Tokenlänge und
Benutzername. `Session.Username` schrieb bei jeder Zuweisung einen vollständigen
Stacktrace ins Debug-Log.

**Behoben:** beides entfernt; `Session` um `Clear()`, `IsAuthenticated` und
`NotifyExpired()` ergänzt.

### S7 ✅ Keine Passwortrichtlinie serverseitig
Der Server akzeptierte jedes nicht-leere Passwort. Die Mindestlänge von 6 Zeichen wurde
nur im Client geprüft und war durch direkte API-Aufrufe umgehbar.

**Behoben:** Mindestens 8 Zeichen, Benutzername 3–24 Zeichen aus `[a-zA-Z0-9_]`,
serverseitig erzwungen. Client-Validierung angeglichen.

---

## F — Funktionale Fehler und Lücken

### F1 ✅ Gruppenchat war nicht benutzbar [verifiziert]
`IChatService.CreateGroupAsync` war deklariert, `ChatApiService` implementierte es und
rief `POST /conversations/group`. Diesen Endpunkt gab es nicht → **404**, die Methode
warf. `JsonChatStore.CreateGroupAsync` existierte serverseitig bereits — es fehlte nur
der Controller. Die Oberfläche bot ohnehin keinen Weg, eine Gruppe anzulegen.

**Behoben:** Endpunkt ergänzt (Ersteller wird automatisch Mitglied, unbekannte
Benutzer-IDs werden abgewiesen, Mindestgröße drei) plus Dialog „Group" im Client.

### F2 ✅ Live-Aktualisierung brach ab Nachricht 51 [verifiziert]
`ChatApiService.GetMessagesAfterAsync` schickte `?afterUtc=…`, der Server kannte nur
`take`/`skip` und ignorierte den Parameter. Zurück kamen immer die **ersten** 50
Nachrichten. Der Client dedupliziert über `MessageId`, weshalb es bei kurzen Chats
funktionierte — sobald eine Konversation mehr als 50 Nachrichten hatte, lagen neue
Nachrichten außerhalb des zurückgelieferten Fensters und **erreichten den Empfänger nie**.
Test vorher: `?afterUtc=<jetzt>` lieferte 2 statt 0 Nachrichten.

**Behoben:** `GetMessagesAfterAsync` im Store und `afterUtc` im Controller. Test mit
61 Nachrichten: die Abfrage liefert exakt die eine neue Nachricht.

### F3 ✅ Paginierung lieferte die ältesten statt der neuesten Nachrichten
`JsonChatStore.GetMessagesAsync` sortierte aufsteigend und nahm dann `Skip/Take`. Beim
Öffnen eines Chats mit 200 Nachrichten sah man die Nachrichten 1–50, nicht die letzten 50.

**Behoben:** absteigend sortieren, `Skip/Take`, dann für die Anzeige wieder
chronologisch. Test mit 60 Nachrichten liefert msg-11 bis msg-60.

### F4 ✅ Kein Logout, keine Reaktion auf abgelaufene Tokens
Das JWT läuft nach 120 Minuten ab. Danach lieferten alle Aufrufe 401,
`ChatApiService.GetJsonAsync` fing das ab und gab `null` zurück — die Oberfläche zeigte
eine leere Kontaktliste ohne Hinweis. Ein Logout existierte nicht.

**Behoben:** Logout-Schaltfläche, die zum Login-Fenster zurückführt statt die Anwendung
zu beenden. Ein abgelehntes Token löst `Session.NotifyExpired()` aus; der Nutzer wird
informiert und zum Login geführt. Im GUI-Durchstich verifiziert.

### F5 ✅ Zeitstempel sprangen zwischen Ladepfad und Polling
Laden und Senden setzten `Time = …ToLocalTime()`, die Polling-Schleife dagegen
`Time = m.SentAtUtc` ohne Konvertierung. Per Polling eingetroffene Nachrichten wurden in
Mitteleuropa ein bis zwei Stunden zu früh angezeigt.

**Behoben:** `MessageModel` führt `Time` (Ortszeit, Anzeige) und `SentAtUtc` (UTC,
Sortierung und Poll-Cursor) getrennt. Alle Pfade gehen durch dieselbe `Append`-Methode.

### F6 ✅ Keine Vorschau in der Kontaktliste
`ContactModel.LastMessage` wurde nur befüllt, wenn die Konversation im laufenden
Programmlauf schon geöffnet war. `ConversationDto.LastMessage` existierte im Client-DTO,
wurde vom Server aber nie gesetzt.

**Behoben:** Server liefert die letzte Nachricht je Konversation mit (eine Abfrage für
alle, nicht eine pro Konversation) und sortiert die Liste nach letzter Aktivität.

### F7 ✅ Nachrichtenliste scrollte nicht automatisch
Neue Nachrichten erschienen unterhalb des sichtbaren Bereichs.

**Behoben:** `ChatViewModel.MessagesAppended` löst `ScrollIntoView` auf das letzte
Element aus.

### F8 ✅ Nicht funktionsfähige Bedienelemente
* Suchfeld in `MainWindow.xaml` — ohne Binding und ohne Funktion
* Anhang-Button (`+`) in `ChatView.xaml` — ohne Command, vom Autor als TODO markiert
* „Forgot Password" zeigte `MessageBox.Show("Forgot your password? Unlucky.")`
* Telefon- und Video-Icons in der Chat-Kopfzeile waren reine Grafik

**Behoben:** entfernt statt Funktion vorzutäuschen, jeweils mit Verweis auf TODO 5.6.
„Forgot Password" nennt den tatsächlichen Stand. An die Stelle der Icons trat eine
Anzeige der letzten Aktivität.

### F9 ✅ Zwei konkurrierende Pfade für das Passwort
Beim Login kam das Passwort aus dem `CommandParameter` (der `PasswordBox`), bei der
Registrierung aus der gebundenen `Password`-Eigenschaft. `Login()` überschrieb zusätzlich
das Backing Field direkt unter Umgehung des Setters.

**Behoben:** nur noch die gebundene Eigenschaft über `PasswordBoxHelper`;
`CommandParameter` entfernt.

### F10 ✅ Netzwerkfehler wurden verschluckt
`ChatApiService.GetJsonAsync` fing jede Exception und gab `null` zurück. Ein
Serverausfall war in der Oberfläche nicht von „keine Konversationen vorhanden"
unterscheidbar.

**Behoben:** `ApiException` unterscheidet Netzwerkausfall, abgelaufenes Token und
Serverfehler; die Chat-Ansicht zeigt einen Hinweisbanner.

### F11 ✅ `async void` in Event-Handlern ohne Fehlerbehandlung
`LoginViewModel.Login` und `.Register` sind `async void`. Eine dort entweichende
Exception beendet den Prozess. Ebenso fehlte ein Ladeindikator.

**Behoben:** beide laufen über einen `RunAsync`-Wrapper mit `try/catch` und `IsBusy`;
Login-Fenster und Chat-Ansicht zeigen den Ladezustand.

### F12 ✅ Zeitgleiche Nachrichten konnten verloren gehen
*Beim Testen der Paginierung gefunden, nicht Teil der ursprünglichen Erhebung.*
Zwei in derselben Millisekunde gesendete Nachrichten erhielten denselben
`SentAtUtc`-Wert. Das machte die Sortierung nichtdeterministisch, und da das
inkrementelle Polling `SentAtUtc > cursor` verwendet, wäre die zweite Nachricht
**dauerhaft nie ausgeliefert** worden.

**Behoben:** `AddMessageAsync` erzwingt je Konversation streng monoton steigende
Zeitstempel (bei Gleichstand plus einen Tick).

---

## A — Architektur

### A1 ⬜ Zwei konkurrierende Persistenzschichten
Für Konversationen und Nachrichten existieren vollständige EF-Core-Entities
(`Conversation`, `ConversationMember`, `Message`), Migrationen sind geschrieben und in
der Datenbank angewendet. Genutzt wird stattdessen `JsonChatStore` mit flachen Dateien.
Damit gibt es keine Fremdschlüssel, keine Transaktionen, keine Indizes und keine
Konsistenz zwischen Benutzern (PostgreSQL) und ihren Nachrichten (JSON). Ein gelöschter
Benutzer hinterlässt verwaiste Nachrichten.

Siehe TODO 4.1.

### A2 🟡 JSON-Ablage nicht crash-sicher und nicht skalierbar
`SaveListAsync` öffnete `new StreamWriter(path, append: false)` — die Datei wurde zuerst
geleert und dann neu geschrieben. Ein Absturz während des Schreibens vernichtete **alle**
Nachrichten. Zusätzlich wird bei jeder einzelnen Nachricht die komplette Datei geladen,
deserialisiert, serialisiert und zurückgeschrieben — O(n) pro Schreibvorgang. Das
`SemaphoreSlim` schützt nur innerhalb eines Prozesses.

**Teilweise behoben:** Schreiben erfolgt jetzt atomar über eine temporäre Datei und
`File.Replace`. Das O(n)-Verhalten und die fehlende Mehrprozessfähigkeit bleiben und
verschwinden erst mit A1.

### A3 ⬜ Polling statt Push
Jeder Client fragt alle 1,2 Sekunden den Server ab. Bei 50 gleichzeitigen Nutzern sind
das rund 42 Anfragen pro Sekunde, die jeweils die komplette `messages.json` einlesen.
SignalR (WebSockets) ist der passende Mechanismus. Siehe TODO 5.1.

### A4 🟡 N+1-Abfragen in den Controllern
`GetConversations` führte pro Teilnehmer, `GetMessages` pro Nachricht ein einzelnes
`FindByIdAsync` aus.

**Teilweise behoben:** Absender werden nur noch einmal je *eindeutigem* Absender
aufgelöst, die Konversationsvorschauen in einem Zug. Die Teilnehmerauflösung ist
weiterhin N+1 — siehe TODO 4.3.

### A5 ⬜ Keine Dependency Injection im Client
Dienste hängen als statische Felder an `App`, der Sitzungszustand in einer statischen
`Session`-Klasse. Dadurch ist kein ViewModel isoliert testbar. Siehe TODO 4.7.

### A6 ⬜ Fehlerhafte EF-Beziehung erzeugt Schatten-Fremdschlüssel
Beim Start protokolliert EF Core:
> The foreign key property `ClientDevice.UserId1` was created in shadow state because a
> conflicting property with the simple name `UserId` exists…

Ursache: `AppDbContext` konfiguriert `ClientDevice.User` mit `HasForeignKey(c => c.UserId)`,
während `User.Clients` eine zweite, nicht konfigurierte Beziehung aufspannt. Die Tabelle
bekommt dadurch eine überflüssige Spalte `UserId1`. Siehe TODO 4.2.

### A7 ⬜ Toter Code
| Datei | Befund |
|-------|--------|
| `Client/MVVM/View/Home.cs` | leere Klasse, Namespace `WPFCalculator.View` — Rest einer anderen Übung |
| `Client/Theme/ChatStyles.xaml` | zeichengleiches Duplikat von `ChatItem.xaml`, nirgends eingebunden |
| `Client/Interface/IServerService.cs` | vollständige Schnittstelle inkl. Result-Typen, ohne Implementierung und ohne Aufrufer |
| `Client/MVVM/ViewModel/HomeViewModel.cs` | leere Klasse (wird aber als DataTemplate-Schlüssel gebraucht) |
| `Server/Service/JsonUserStore.cs` | durch `DatabaseUserStore` ersetzt, nicht registriert |
| `Server/Models/ClientDevice.cs` | Entity und Tabelle, nie gelesen oder geschrieben |

Siehe TODO 4.6.

### A8 ⬜ DTOs doppelt gepflegt
`ConversationDto`, `UserDto`, `MessagePreviewDto` und `MessageDto` existieren unabhängig
im Server-Controller und im Client-Interface. Jede Änderung muss an beiden Stellen
nachgezogen werden; Abweichungen fallen erst zur Laufzeit als stille `null`-Werte auf —
genau das war die Ursache von F6. Der `ConversationsController` liegt zudem im globalen
Namespace statt in `Server.Controllers`. Siehe TODO 4.4 und 4.5.

---

## D — Build und DevOps

### D1 ✅ `packages.config` war unvollständig
`Client/WPF_Fusion.csproj` referenziert `System.Text.Json` und
`System.Text.Encodings.Web`, beide fehlten in `Client/packages.config`. Ein
`nuget restore` hätte sie nicht heruntergeladen und der Build wäre mit nicht auflösbaren
Referenzen gescheitert. Es fiel nicht auf, weil der komplette `packages/`-Ordner
mit eingecheckt war.

**Behoben:** beide Pakete eingetragen.

### D2 ✅ Kein `.gitignore`, rund 410 MB Ballast
Das Verzeichnis umfasste etwa 517 MB, davon:

| Artefakt | Größe |
|----------|-------|
| `Fusion_lokal_260224/` (vollständige zweite Kopie des Projekts) | 112 MB |
| sechs ZIP-Sicherungen im Wurzelverzeichnis | 306 MB |
| `Server/out/`, `out/out/`, `out/out/out/` (verschachtelte Publish-Ausgaben) | — |
| `bin/`, `obj/`, `packages/` | — |

**Behoben:** `.gitignore` schließt Build-Ausgaben, Pakete, Archive, den Duplikat-Baum,
lokale Konfigurationsdateien und die zur Laufzeit geschriebenen Chat-Dateien aus. Die
Dateien bleiben auf der Platte, gelangen aber nicht in die Versionierung — und damit
auch nicht die darin enthaltenen alten Zugangsdaten aus S2.

### D3 ⬜ Gemischte Zielplattformen
Client auf .NET Framework 4.8 (Windows-only, `packages.config`-Toolchain, nur mit
Visual-Studio-MSBuild baubar), Server auf .NET 8 (`dotnet build`). Ein Umstieg des
Clients auf `net8.0-windows` vereinheitlicht die Werkzeugkette, ermöglicht `dotnet test`
und einen einzigen CI-Lauf. Siehe TODO 5.3.

### D4 🟡 Keine Tests, keine CI, kein Compose-Setup
**Teilweise behoben:** `docker-compose.yml` mit Volume und Healthcheck ergänzt. Ein
Testprojekt und eine CI-Pipeline fehlen weiterhin — siehe TODO 5.2 und 5.4. Die während
des Umbaus verwendeten PowerShell-Smoke-Tests (39 Prüfungen) decken die kritischen
Pfade bereits ab und eignen sich als Vorlage für xUnit-Tests.

### D5 ✅ Dev-Profil band an alle Netzwerkschnittstellen
`launchSettings.json`, Profil `https`: `"applicationUrl": "http://0.0.0.0:5214;https://0.0.0.0:7214"`
— der Entwicklungsserver war damit inklusive Swagger und Stacktraces im gesamten
lokalen Netz erreichbar.

**Behoben:** bindet an `127.0.0.1`.

### D6 ⬜ Compiler-Warnungen
Der Client-Build erzeugt rund 20 Nullable-Warnungen (CS8618/CS8601/CS8625).
`<Nullable>enable</Nullable>` ist aktiviert, aber nicht konsequent umgesetzt. Warnungen
in dieser Menge machen echte Hinweise unsichtbar. Siehe TODO 4.8.

---

## Was gut gelöst war

Damit das Bild nicht schief hängt — diese Stellen sind solide und wurden bewusst nicht
angetastet:

* **Passwort-Hashing** (`PasswordHasher`): PBKDF2-HMAC-SHA256, 210.000 Iterationen,
  128-Bit-Zufallssalt pro Passwort, versioniertes Speicherformat und ein
  laufzeitkonstanter Vergleich, der bewusst nicht bei Längenunterschied abbricht.
  Lehrbuchreif.
* **Trennung Client/Server**: Passwörter werden ausschließlich serverseitig gehasht,
  der Client sieht nie einen Hash.
* **MVVM-Disziplin**: Views ohne Geschäftslogik, `IDialogService` + `IDialogRequestClose`
  für ViewModel-getriebene Dialoge, `PasswordBoxHelper` als saubere
  Attached-Property-Lösung. Der neue Gruppen-Dialog konnte diesem Muster ohne Umwege
  folgen.
* **Idempotenter Direktchat**: `GetOrCreateDirectAsync` mit reihenfolgeunabhängigem
  Paar-Schlüssel — zweimaliger Aufruf liefert dieselbe Konversation (verifiziert).
* **Robuste Claim-Auflösung**: `GetUserIdFromClaims` akzeptiert mehrere übliche
  Claim-Bezeichner.
* **XML-Dokumentationskommentare** auf Controllern, Services und Schnittstellen.

---

## Prüfumfang

Die Behebung wurde mit drei Testläufen abgesichert:

| Lauf | Umfang | Ergebnis |
|------|--------|----------|
| Stufe 1–2 (API) | Registrierung, Login, Falschpasswort, manipuliertes Token, Passwortregeln, Zugriffsschutz, Rate-Limiting | 22/22 |
| Stufe 3 (API) | Paginierungsrichtung, `afterUtc`-Grenzfälle, Vorschau, Gruppenchat, Zugriffsschutz für Gruppen | 17/17 |
| GUI-Durchstich | Login → MainWindow → Chat-Ansicht → Kontakt anlegen → Nachricht senden → Gegenprobe beim Empfänger → Logout | bestanden |
