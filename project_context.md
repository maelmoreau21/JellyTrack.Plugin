*** Begin Patch
*** End Patch
#+ NOTE: Ce fichier a été mis à jour pour s'aligner stylistiquement et contenutiellement sur `JellyTrack/project_context.md`. Voir la section "Vérifications de cohérence" ci‑dessous.

# JellyTrack Plugin — Contexte, contrat d'événements et guide pour agents IA

IMPORTANT (pour agents IA) — lire entièrement ce document avant de proposer des modifications :
- Ne pas « halluciner » formats de payload, champs d'événements ou clés i18n. Toujours vérifier les sources canoniques dans le dépôt : `JellyTrack.Plugin/Models/*.cs`, `JellyTrack.Plugin/Notifiers/*`, et le serveur `src/app/api/plugin/events/route.ts` dans l'application.
- Respectez les conventions du projet parent (voir `../project_context.md`) pour l'alignement des endpoints, des intervalles et des clés globales (`pluginApiKey`).

## 1. Vue d'ensemble

Le plugin est l'émetteur des événements Jellyfin vers l'application JellyTrack (Next.js + PostgreSQL + Redis). Il capte les événements locaux (playback, progress, heartbeat, library change) et les pousse via HTTP POST vers l'endpoint configuré sur le serveur (par défaut `/api/plugin/events`).

Objectif principal : fournir un flux d'événements fiable, idempotent et peu intrusif pour le serveur JellyTrack.

## 2. Stack technique (résumé)
- Langage : C# (.NET 8.0)
- Pattern : `IEventConsumer<T>`, `IScheduledTask`, `IHostedService` pour tâches périodiques
- Sérialisation : `System.Text.Json` (format JSON, champs standard)
- Réseau : `HttpClient` via `IHttpClientFactory` (singleton)

## 3. Diagramme simplifié
```
Jellyfin Server
  └── JellyTrack Plugin (.dll)
        ├── Notifiers (PlaybackStart/Stop/Progress)
        ├── HeartbeatService (périodique)
        └── LibraryChangeNotifier (debounced)
              │
              ▼
        JellyTrackApiClient (HttpClient)
              │
              ▼  POST → /api/plugin/events  (par défaut)
        JellyTrack Server (Next.js)
```

## 4. Événements envoyés (contrat minimal)
Les types d'événements et leurs rôles :
- `Heartbeat` (default 60s) : état du plugin, version, `serverLanguage`, nom du serveur, liste d'utilisateurs pour synchronisation.
- `PlaybackStart` : début de session — metadata complète du média + session (sessionId, userId, client, device, ip, playMethod, duration, codecs, etc.).
- `PlaybackProgress` (default 15s) : position actuelle (positionMs), état pause/play, index des pistes audio/sous-titres, flags de changement (audio/subtitle change).
- `PlaybackStop` : fin de session — position finale, durée totale consommée.
- `LibraryChanged` : signal d'ajout/modification de médias — envoyé en batch avec debounce (~30s) pour éviter rafales.

Remarque : les noms exacts et la structure des payloads sont définis par `JellyTrack.Plugin/Models/*.cs` (ex : `PlaybackStartEvent.cs`). Toute modification du contrat doit être coordonnée avec le serveur JellyTrack.

## 5. Sécurité & Auth
- Authentification : header `Authorization: Bearer {apiKey}` (clé stockée côté serveur dans `global_settings.pluginApiKey`).
- Timeout HTTP : typiquement 5s par requête pour ne pas bloquer Jellyfin.
- Le plugin ne doit jamais stocker les secrets en clair en dehors de la configuration Jellyfin.

## 6. Robustesse & résilience
- Queue mémoire (taille configurable, ex. max 100 évènements) pour buffer des envois.
- Retry backoff local sur échecs réseau ; en cas d'échec soutenu, retention minimale puis purge pour éviter usage mémoire illimité.
- Debounce/batching pour `LibraryChanged`.

## 7. Mapping côté serveur (rappel opérationnel)
Le serveur JellyTrack attend ces comportements côté plugin :
- Vérification d'API key : le serveur compare `Authorization` à `global_settings.pluginApiKey`.
- Upserts canonical : `upsertCanonicalUser` et `upsertCanonicalMedia` sont déclenchés à la réception de `PlaybackStart` / `LibraryChanged`.
- `PlaybackStart` crée ou réouvre une `playback_history` (si fenêtre de merge trouvée), `PlaybackProgress` écrit des `telemetry_event` (batch via `createMany`), `PlaybackStop` finalise `duration_watched`.
- Active stream snapshot : le plugin peut être source d'un `stream:<sessionId>` en Redis (TTL court) si configuré.

Ces attentes sont documentées dans l'application (voir `JellyTrack/project_context.md` → section ingestion/pipeline). Avant de modifier le format d'un événement, mettez à jour les deux documents et le code serveur.

## 8. Internationalisation
- Le plugin envoie `serverLanguage` dans `Heartbeat` (format ISO court, ex. `en`, `fr`, `pt-BR`).
- Si le plugin propose une option `PreferredLanguage`, le champ est utilisé à la place de la langue du serveur.

## 9. Configuration & options
- URL de destination (endpoint) — configurable dans la page admin du plugin (ex: `/api/plugin/events`).
- API key — `pluginApiKey` côté serveur : obligatoire si activé.
- Intervalles : heartbeat (par défaut 60s), playback progress (15s), debounce library (≈30s). Ces valeurs sont configurables.

## 10. Structure des fichiers (rapide)
```
JellyTrack.Plugin/
├── JellyTrack.Plugin.csproj
├── Plugin.cs
├── PluginConfiguration.cs
├── Configuration/configPage.html
├── Notifiers/ (PlaybackStart/Stop/Progress)
├── Services/ (JellyTrackApiClient, HeartbeatService, LibraryChangeNotifier)
├── Models/ (PluginEvent, PlaybackStartEvent, PlaybackStopEvent, PlaybackProgressEvent, LibraryChangedEvent, HeartbeatEvent)
└── build.yaml
```

## 11. Dépendances
- Jellyfin.Controller, Jellyfin.Model (10.10.x)
- .NET 8.0 runtime

## 12. Scripts et build
- Build plugin : `dotnet build` (dans le dossier `JellyTrack.Plugin`).
- Tests/custom scripts : vérifier la CI interne si présent (ex. build.yaml).

## 13. Checklist PR / procédure avant changement de contrat
1. Modifier d'abord `JellyTrack.Plugin/Models/*.cs` et documenter le nouveau contrat dans ce fichier.
2. Mettre à jour le serveur JellyTrack (`src/app/api/plugin/events/route.ts` et code d'ingestion) pour accepter le nouveau payload.
3. Mettre à jour `JellyTrack/project_context.md` et `JellyTrack.Plugin/project_context.md` en parallèle.
4. Exécuter `dotnet build` (plugin) et `npm run build` (application) et corriger les erreurs.
5. Ajouter/mettre à jour les tests d'intégration (si disponibles) et les runbooks de rollback.

## 14. Vérifications de cohérence (comparaison rapide avec l'application)
- Endpoint d'ingestion : **/api/plugin/events** — OK (app et plugin sont alignés).
- Méthode d'authentification : **Authorization: Bearer {apiKey}** et `global_settings.pluginApiKey` côté serveur — OK.
- Intervalles par défaut : Heartbeat 60s, Progress 15s — OK.
- `serverLanguage` inclus dans `Heartbeat` : plugin envoie ce champ ; côté serveur le champ est toléré/consommé — OK.
- Format JSON : `System.Text.Json` côté plugin et parsing côté serveur (Next.js) — OK, faire attention aux BigInt (positionMs) qui doivent être sérialisés/précisés si nécessaire (le serveur attend parfois des strings pour BigInt).

Aucun écart critique trouvé lors de la revue — voir checklist ci‑dessous en cas d'évolution du contrat.

## 15. Notes pour agents IA
- Toujours vérifier les fichiers modèles côté plugin (`JellyTrack.Plugin/Models/*.cs`) et le route handler côté serveur (`JellyTrack/src/app/api/plugin/events/route.ts`) avant de proposer des modifications au contrat.
- Ne pas modifier seul le format d'un événement : prévenez et coordonnez les changements sur les deux dépôts.

---

Si vous voulez, j'ajoute ici un exemple de payload `PlaybackProgress` et un diagramme mermaid expliquant l'idempotence et la fenêtre de merge côté serveur.
