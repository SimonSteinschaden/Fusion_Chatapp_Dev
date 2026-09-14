# TODO — Weg zur brauchbaren Chat-Anwendung

Priorisierte Arbeitsliste. Die IDs verweisen auf [docs/FINDINGS.md](docs/FINDINGS.md).

**Stand 14.09.2026:** Stufen 1–3 sind abgeschlossen und verifiziert
(39 API-Prüfungen plus ein durchgehender GUI-Durchstich). Stufen 4–5 stehen aus.

---

## ✅ Stufe 1 — Lauffähig machen  *(abgeschlossen)*

- [x] **1.1** Verbindungszeichenfolge wird beim Start protokolliert (ohne Passwort);
      ist die Datenbank nicht erreichbar, bricht der Server mit einer Meldung ab,
      die ausdrücklich auf die Umgebungsvariable hinweist.  → B1
- [x] **1.2** `Database.Migrate()` beim Start, abschaltbar über `Database:AutoMigrate`.
      Verifiziert gegen eine leere Datenbank: vier Migrationen wurden angewendet.  → B4
- [x] **1.3** Server-Adresse des Clients kommt aus `App.config` (`ApiBaseUrl`).  → B2
- [x] **1.4** `UserApiService.GetMeAsync()` ruft `users/me` statt `me`.  → B3
- [x] **1.5** Debug-`MessageBox` und Stacktrace-Logging entfernt.  → S6
- [x] **1.6** `docker-compose.yml` mit Volume und Healthcheck.  → D4
- [x] **1.7** `.gitignore` angelegt.  → D2
- [x] **1.8** Fehlende NuGet-Einträge in `packages.config` ergänzt
      (`System.Text.Json`, `System.Text.Encodings.Web`).  → D1

**Abnahme bestanden:** leere Datenbank → `docker compose up -d` → `dotnet run` →
Client → Registrieren, Einloggen, Nachricht senden, ohne manuelle Eingriffe.

---

## ✅ Stufe 2 — Sicherheit  *(abgeschlossen)*

- [x] **2.1** Teilnehmerprüfung in `GetMessages` **und** `SendMessage`. Nichtteilnehmer
      erhalten `404` statt `403`, damit der Endpunkt die Existenz einer
      Konversations-ID nicht verrät.  → S1
- [x] **2.2** `Jwt:Key` und Verbindungszeichenfolge aus `appsettings.json` entfernt;
      Entwicklung über User Secrets, Betrieb über Umgebungsvariablen.  → S2
- [x] **2.3** Neuer JWT-Signaturschlüssel erzeugt (384 Bit).  → S2
- [x] **2.4** `appsettings-SimonPC.json` gelöscht.  → S2
- [x] **2.5** Swagger und Developer Exception Page nur noch in Development;
      sonst `UseExceptionHandler` mit generischer Fehlerantwort.  → S4
- [x] **2.6** HSTS und HTTPS-Weiterleitung außerhalb von Development aktiv.  → S3
- [x] **2.7** Rate-Limiting auf den Anmeldeendpunkten: 10 Anfragen pro Minute,
      getrennt je IP **und** Endpunkt, mit `Retry-After`-Header.  → S5
- [x] **2.8** Passwort- und Benutzernamensregeln serverseitig durchgesetzt;
      Client-Validierung angeglichen.  → S7
- [x] **2.9** `launchSettings.json` bindet `127.0.0.1` statt `0.0.0.0`.  → D5

**Abnahme bestanden:** Fremdzugriff auf eine fremde Konversation liefert `404`;
keine Zugangsdaten mehr im versionierten Stand.

> **Offen, außerhalb des Codes:** Das Passwort des PostgreSQL-Kontos auf
> `srv1266377.hstgr.cloud` muss noch am Server selbst geändert werden — der alte
> Wert steht in sechs ZIP-Archiven auf der Platte.

---

## ✅ Stufe 3 — Chat wirklich brauchbar  *(abgeschlossen)*

- [x] **3.1** `GET /conversations/{id}/messages?afterUtc=` liefert nur Neueres.  → F2
- [x] **3.2** Paginierung liefert die **neuesten** `take` Nachrichten,
      chronologisch sortiert.  → F3
- [x] **3.3** `POST /conversations/group` samt Dialog „Group" im Client. Der Ersteller
      wird serverseitig ergänzt, unbekannte Benutzer-IDs werden abgewiesen.  → F1
- [x] **3.4** Polling verwendet Ortszeit für die Anzeige und UTC für den Abgleich;
      `MessageModel` führt beides getrennt.  → F5
- [x] **3.5** Auto-Scroll ans Ende bei neuen Nachrichten.  → F7
- [x] **3.6** `ConversationDto.LastMessage` wird serverseitig befüllt; die Liste ist
      nach letzter Aktivität sortiert.  → F6
- [x] **3.7** Logout kehrt zum Login-Fenster zurück, ohne die Anwendung zu beenden.  → F4
- [x] **3.8** Ein abgelehntes Token löst `Session.NotifyExpired()` aus; der Nutzer wird
      informiert und zum Login geführt.  → F4
- [x] **3.9** Verbindungsfehler erscheinen als Banner statt als leere Liste;
      `ApiException` unterscheidet Netzwerkausfall, abgelaufenes Token und Serverfehler.  → F10
- [x] **3.10** Ladeanzeige in Login und Chat; `async void`-Handler sind abgesichert.  → F11
- [x] **3.11** Passwort kommt nur noch aus der gebundenen Eigenschaft;
      `CommandParameter` entfernt.  → F9
- [x] **3.12** Nicht funktionsfähige Bedienelemente entfernt (Suchfeld, Anhang-Button,
      Telefon/Video); „Forgot Password" sagt jetzt die Wahrheit.  → F8
- [x] **3.13** *(nicht geplant, beim Testen gefunden)* Zeitstempel sind je Konversation
      streng monoton. Zuvor konnten zwei Nachrichten derselben Millisekunde denselben
      Zeitstempel bekommen — beim Polling (`SentAtUtc > cursor`) wäre die zweite
      dauerhaft verschluckt worden.
- [x] **3.14** *(vorgezogen aus Stufe 4)* `JsonChatStore` schreibt atomar über eine
      temporäre Datei. Zuvor wurde die Datei erst geleert und dann neu beschrieben —
      ein Absturz mittendrin hätte alle Nachrichten vernichtet.  → A2 (teilweise)

**Abnahme bestanden:** 60 Nachrichten hin und her; Paginierung zeigt Nachricht 11–60,
`afterUtc` liefert exakt die eine neue Nachricht. GUI-Durchstich: Login → Kontakt
anlegen → senden → Empfänger sieht sie.

---

## ⬜ Stufe 4 — Architektur bereinigen  ⏱ ~6–10 h

- [ ] **4.1** **Chat von JSON auf PostgreSQL umstellen** — `EfChatStore` als
      `IChatStore`-Implementierung, Entities und Migrationen sind vorhanden.
      Einmaliges Migrationsskript für bestehende `messages.json`.  → A1, A2
- [ ] **4.2** `ClientDevice`-Beziehung in `AppDbContext` korrigieren
      (`HasOne(c => c.User).WithMany(u => u.Clients)`), Schatten-FK `UserId1`
      per Migration entfernen. Die Warnung erscheint bei jedem Start.  → A6
- [ ] **4.3** N+1 beseitigen: `IUserStore.FindManyByIdsAsync` und Auflösung per
      `WHERE Id IN (…)`. Die Nachrichtenliste löst Absender inzwischen nur noch
      einmal je Absender auf, die Teilnehmerauflösung ist aber weiterhin N+1.  → A4
- [ ] **4.4** Gemeinsames Projekt `Fusion.Contracts` für die DTOs, von Client und
      Server referenziert.  → A8
- [ ] **4.5** `ConversationsController` nach `Server.Controllers` verschieben.  → A8
- [ ] **4.6** Toten Code löschen: `Home.cs`, `ChatStyles.xaml`, `IServerService.cs`,
      `JsonUserStore.cs`; über `ClientDevice` entscheiden.  → A7
- [ ] **4.7** DI im Client einführen (`Microsoft.Extensions.DependencyInjection`),
      `Session` als injizierbaren `ISessionService`.  → A5
- [ ] **4.8** Nullable-Warnungen abarbeiten und `<TreatWarningsAsErrors>` aktivieren.  → D6

---

## ⬜ Stufe 5 — Reifegrad  ⏱ ~4–8 h

- [ ] **5.1** **SignalR statt Polling** — Hub `/hubs/chat`, Nachrichten werden an
      Gruppenmitglieder gepusht. Polling als Rückfallebene behalten.  → A3
- [ ] **5.2** Testprojekt `Server.Tests` (xUnit). Die PowerShell-Smoke-Tests aus der
      Umbauphase decken die Fälle bereits ab und sind eine gute Vorlage:
      `PasswordHasher`, Teilnehmerprüfung, `GetOrCreateDirect`-Idempotenz,
      Paginierungsrichtung, `afterUtc`-Grenzfälle.  → D4
- [ ] **5.3** Client auf `net8.0-windows` und SDK-Projektformat migrieren;
      `packages.config` durch `PackageReference` ersetzen.  → D3
- [ ] **5.4** GitHub-Action: Server bauen + Tests, Client bauen (windows-latest).  → D4
- [ ] **5.5** Strukturiertes Logging (Serilog) mit Korrelations-IDs.
- [ ] **5.6** Fehlende Chat-Funktionen: Nachrichten bearbeiten/löschen, Gelesen-Status,
      Tippt-gerade-Anzeige, Dateianhänge, Benutzersuche, Sprach-/Videoanruf.
- [ ] **5.7** Kontosperre nach zu vielen Fehlversuchen. Das Rate-Limiting begrenzt
      pro IP, nicht pro Konto — ein verteilter Angriff umgeht es.
