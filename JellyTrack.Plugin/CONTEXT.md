# JellyTrack Plugin — Context & Architecture

## Vue d'ensemble

**JellyTrack** est un tableau de bord analytique autonome pour Jellyfin (équivalent de Tautulli pour Plex). Ce plugin est la **seule source de données** pour les sessions de lecture — il n'y a ni polling ni webhook externe.

Le plugin intercepte les événements Jellyfin en temps réel et les pousse vers l'application JellyTrack (Next.js / PostgreSQL / Redis) via une API REST sécurisée.

## Architecture du plugin

### Communication

```
Jellyfin Server
  └── JellyTrack Plugin (.dll)
        ├── Notifiers (PlaybackStart, Stop, Progress)
        ├── HeartbeatService (périodique)
        └── LibraryChangeNotifier (ajout/modification)
              │
              ▼
        JellyTrackApiClient (HttpClient)
              │
              ▼  POST /api/plugin/events
        JellyTrack Server (Next.js)
```

### Flux de données

1. **Heartbeat** (toutes les 60s par défaut) : signale que le plugin est actif, envoie la version du plugin, le nom du serveur Jellyfin, la version Jellyfin, et la **liste complète des utilisateurs** pour synchronisation.

2. **PlaybackStart** : déclenché quand un utilisateur commence une lecture. Envoie les métadonnées complètes du média (titre, type, genres, résolution, durée, codecs…) + infos session (client, device, IP, méthode de lecture).

3. **PlaybackProgress** (toutes les 15s par défaut) : envoie la position actuelle, l'état pause/play, et les index des pistes audio/sous-titres pour détecter les changements.

4. **PlaybackStop** : déclenché à l'arrêt de la lecture. Envoie la position finale et la durée totale.

5. **LibraryChanged** : déclenché lors de l'ajout/modification de médias. Utilise un mécanisme de **debounce/batch** (30s) pour éviter les rafales lors des scans de bibliothèque.

### Sécurité

- Authentification par clé API (format `jt_xxxxxxxxxxxx`) via header `Authorization: Bearer {apiKey}`.
- Aucune donnée stockée côté Jellyfin — tout est poussé vers JellyTrack.
- Timeout HTTP de 5 secondes pour ne pas bloquer Jellyfin.

### Gestion des erreurs

- File d'attente en mémoire (max 100 événements) pour les envois échoués.
- Retry automatique lors du prochain événement.
- Logging via `ILogger<T>` (Debug/Warning/Error).
- Ne bloque jamais le thread principal de Jellyfin (tout est async).

## Choix techniques

| Choix | Justification |
|-------|---------------|
| `IEventConsumer<T>` | Pattern moderne Jellyfin pour les hooks d'événements |
| `IScheduledTask` | Pour le heartbeat périodique, intégré au scheduler Jellyfin |
| `IHostedService` | Pour le LibraryChangeNotifier avec debounce |
| `HttpClient` singleton | Évite l'épuisement des sockets (via IHttpClientFactory) |
| `System.Text.Json` | Sérialiseur natif .NET, pas de dépendance externe |
| Debounce 30s pour Library | Évite les rafales lors des scans massifs |

## Dépendances

- **Jellyfin.Controller** (10.10.x) — SDK pour les services et événements
- **Jellyfin.Model** (10.10.x) — Modèles de données Jellyfin
- **Target Framework** : .NET 8.0

## Configuration

Toute la configuration est dans l'admin Jellyfin :
- URL de JellyTrack (endpoint API)
- Clé API
- Intervalle heartbeat (défaut: 60s)
- Intervalle PlaybackProgress (défaut: 15s)
- Toggle activer/désactiver

## Structure des fichiers

```
JellyTrack.Plugin/
├── CONTEXT.md                          # Ce fichier
├── JellyTrack.Plugin.csproj            # Projet .NET
├── Plugin.cs                           # Point d'entrée du plugin
├── PluginConfiguration.cs              # Configuration
├── Configuration/
│   └── configPage.html                 # Page de configuration web
├── Notifiers/
│   ├── PlaybackStartNotifier.cs        # Hook PlaybackStart
│   ├── PlaybackStopNotifier.cs         # Hook PlaybackStop
│   └── PlaybackProgressNotifier.cs     # Hook PlaybackProgress
├── Services/
│   ├── JellyTrackApiClient.cs          # Client HTTP
│   ├── HeartbeatService.cs             # Heartbeat périodique
│   └── LibraryChangeNotifier.cs        # Changements bibliothèque
├── Models/
│   ├── PluginEvent.cs                  # Modèle de base
│   ├── PlaybackStartEvent.cs           # Événement PlaybackStart
│   ├── PlaybackStopEvent.cs            # Événement PlaybackStop
│   ├── PlaybackProgressEvent.cs        # Événement PlaybackProgress
│   ├── LibraryChangedEvent.cs          # Événement LibraryChanged
│   └── HeartbeatEvent.cs               # Événement Heartbeat
└── build.yaml                          # Métadonnées plugin
```
