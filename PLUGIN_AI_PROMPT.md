# Prompt pour créer le Plugin Jellyfin "JellyTrack"

> **Ce fichier contient un prompt complet et détaillé à fournir à une IA pour qu'elle génère le plugin Jellyfin en C#.**
> Copiez tout le contenu ci-dessous (à partir de la ligne "---") dans votre conversation avec l'IA.

---

## Contexte

Je développe **JellyTrack**, un tableau de bord analytique autonome pour Jellyfin (équivalent de Tautulli pour Plex). L'application est en **Next.js 16+ / PostgreSQL / Redis** et tourne en Docker.

**JellyTrack fonctionne exclusivement via un plugin Jellyfin** qui pousse les événements en temps réel. Il n'y a pas de polling ni de webhook externe — le plugin est la seule source de données pour les sessions de lecture.

Le plugin communique via une **API REST sécurisée par clé API** vers un unique endpoint.

## Ta mission

Crée un **plugin Jellyfin complet en C#** (.NET 8.0+) qui :
1. S'installe dans Jellyfin comme un plugin standard
2. Se configure via l'interface d'administration de Jellyfin (URL de JellyTrack + clé API)
3. Intercepte les événements de lecture et les envoie à JellyTrack en temps réel
4. Envoie un heartbeat périodique pour signaler que le plugin est actif
5. Envoie les changements de bibliothèque (ajout/modification de médias)
6. Synchronise la liste des utilisateurs Jellyfin

**IMPORTANT** : Crée d'abord un fichier `CONTEXT.md` dans le repo du plugin qui documente l'architecture, les choix techniques et le flow de données. Ce fichier servira de référence pour toute maintenance future.

## Architecture requise

### Structure du projet
```
JellyTrack.Plugin/
├── CONTEXT.md                          # Documentation technique du plugin
├── JellyTrack.Plugin.csproj            # Projet .NET (target Jellyfin SDK)
├── Plugin.cs                           # Point d'entrée du plugin
├── PluginConfiguration.cs              # Configuration (URL, API key)
├── Configuration/
│   └── configPage.html                 # Page de configuration web dans Jellyfin
├── Notifiers/
│   ├── PlaybackStartNotifier.cs        # Hook sur PlaybackStart
│   ├── PlaybackStopNotifier.cs         # Hook sur PlaybackStop
│   └── PlaybackProgressNotifier.cs     # Hook sur PlaybackProgress
├── Services/
│   ├── JellyTrackApiClient.cs          # Client HTTP qui envoie les données à JellyTrack
│   ├── HeartbeatService.cs             # Service périodique de heartbeat
│   └── LibraryChangeNotifier.cs        # Hook sur les changements de bibliothèque
├── Models/
│   ├── PluginEvent.cs                  # Modèle de base des événements
│   ├── PlaybackStartEvent.cs           # Modèle PlaybackStart
│   ├── PlaybackStopEvent.cs            # Modèle PlaybackStop
│   ├── PlaybackProgressEvent.cs        # Modèle PlaybackProgress
│   ├── LibraryChangedEvent.cs          # Modèle LibraryChanged
│   └── HeartbeatEvent.cs               # Modèle Heartbeat
└── build.yaml                          # Métadonnées pour le repository de plugins Jellyfin
```

### Configuration du plugin

Le plugin doit avoir une page de configuration dans l'admin Jellyfin avec :
- **URL de JellyTrack** : ex. `http://192.168.1.100:3000/api/plugin/events`
- **Clé API** : la clé générée dans les paramètres de JellyTrack (format: `jt_xxxxxxxxxxxx`)
- **Bouton "Tester la connexion"** : envoie un Heartbeat et affiche le résultat
- **Intervalle de heartbeat** : en secondes (défaut: 60)
- **Intervalle de PlaybackProgress** : en secondes (défaut: 15)
- **Activer/Désactiver le plugin** : toggle global

### Endpoint API de JellyTrack

Le plugin envoie TOUS ses événements vers une seule URL :
```
POST {JellyTrackURL}/api/plugin/events
```

**Authentification** : Header `Authorization: Bearer {apiKey}` ou header `X-Api-Key: {apiKey}`

**Content-Type** : `application/json`

### Format des événements

#### 1. Heartbeat (périodique, toutes les 60s par défaut)
```json
{
  "event": "Heartbeat",
  "pluginVersion": "1.0.0",
  "serverName": "Mon Serveur Jellyfin",
  "jellyfinVersion": "10.9.0",
  "users": [
    {
      "jellyfinUserId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "username": "JohnDoe"
    },
    {
      "jellyfinUserId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
      "username": "JaneDoe"
    }
  ]
}
```

> **Important** : Le heartbeat inclut la liste complète des utilisateurs Jellyfin à chaque envoi. JellyTrack utilisera cette liste pour créer/mettre à jour automatiquement les utilisateurs en base de données. C'est le mécanisme principal de synchronisation des utilisateurs.

#### 2. PlaybackStart
```json
{
  "event": "PlaybackStart",
  "timestamp": "2026-03-11T15:30:00Z",
  "user": {
    "jellyfinUserId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "username": "JohnDoe"
  },
  "media": {
    "jellyfinMediaId": "f9e8d7c6-b5a4-3210-fedc-ba9876543210",
    "title": "Breaking Bad S01E01",
    "type": "Episode",
    "collectionType": "tvshows",
    "seriesName": "Breaking Bad",
    "seasonName": "Saison 1",
    "albumName": null,
    "albumArtist": null,
    "genres": ["Drama", "Thriller"],
    "resolution": "1080p",
    "durationMs": 3480000,
    "parentId": "season-uuid-here",
    "libraryName": "Séries TV",
    "artist": null
  },
  "session": {
    "sessionId": "jellyfin-session-id-here",
    "clientName": "Jellyfin Web",
    "deviceName": "Chrome",
    "playMethod": "DirectPlay",
    "ipAddress": "192.168.1.50",
    "videoCodec": "h264",
    "audioCodec": "aac",
    "audioLanguage": "fre",
    "subtitleLanguage": "eng",
    "subtitleCodec": "srt",
    "transcodeFps": null,
    "bitrate": null,
    "positionTicks": 0
  }
}
```

#### 3. PlaybackStop
```json
{
  "event": "PlaybackStop",
  "timestamp": "2026-03-11T16:28:00Z",
  "sessionId": "jellyfin-session-id-here",
  "user": {
    "jellyfinUserId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  },
  "media": {
    "jellyfinMediaId": "f9e8d7c6-b5a4-3210-fedc-ba9876543210"
  },
  "positionTicks": 348000000000,
  "durationTicks": 348000000000
}
```

#### 4. PlaybackProgress (envoyé périodiquement pendant la lecture, toutes les 15s)
```json
{
  "event": "PlaybackProgress",
  "timestamp": "2026-03-11T15:45:00Z",
  "sessionId": "jellyfin-session-id-here",
  "user": {
    "jellyfinUserId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  },
  "media": {
    "jellyfinMediaId": "f9e8d7c6-b5a4-3210-fedc-ba9876543210"
  },
  "positionTicks": 174000000000,
  "isPaused": false,
  "audioStreamIndex": 1,
  "subtitleStreamIndex": 2
}
```

#### 5. LibraryChanged (à chaque ajout/modification de média dans la bibliothèque)
```json
{
  "event": "LibraryChanged",
  "timestamp": "2026-03-11T20:00:00Z",
  "items": [
    {
      "jellyfinMediaId": "new-movie-uuid",
      "title": "Inception",
      "type": "Movie",
      "collectionType": "movies",
      "genres": ["Sci-Fi", "Action"],
      "resolution": "4K",
      "durationMs": 8880000,
      "parentId": null,
      "libraryName": "Films",
      "artist": null
    }
  ]
}
```

### Détails techniques pour l'extraction des données

Le plugin a accès au contexte Jellyfin complet. Voici comment extraire chaque champ :

#### Depuis `PlaybackStartEventArgs` / `PlaybackProgressEventArgs` / `PlaybackStopEventArgs` :
- `e.Users[0]` → UserId (.Id), Username (.Username)
- `e.Item` → BaseItem (l'élément en cours de lecture)
- `e.Session` → SessionInfo complète
- `e.PlaybackPositionTicks` → position actuelle

#### Depuis `BaseItem` (e.Item) :
- `.Id` → jellyfinMediaId (GUID, le convertir en string)
- `.Name` → title
- `.GetType().Name` ou `.MediaType` → type ("Episode", "Movie", "Audio", etc.)
- `.GetParent()` → parentId
- Pour les épisodes :
  - Caster en `Episode` : `.Series?.Name` → seriesName, `.Season?.Name` → seasonName, `.Season?.Id` → parentId
- Pour la musique :
  - Caster en `Audio` : `.Album` → albumName, `.AlbumArtists` → albumArtist
- `.Genres` → tableau de genres
- `.RunTimeTicks` → durée en ticks (diviser par 10000 pour milliseconds → `durationMs`)
- Pour la résolution : accéder aux MediaStreams vidéo :
  ```csharp
  var videoStream = item.GetMediaStreams()?.FirstOrDefault(s => s.Type == MediaStreamType.Video);
  if (videoStream != null) {
      int width = videoStream.Width ?? 0;
      resolution = width >= 3800 ? "4K" : width >= 1900 ? "1080p" : width >= 1200 ? "720p" : "SD";
  }
  ```

#### Depuis `SessionInfo` (e.Session) :
- `.Id` → sessionId
- `.Client` → clientName  
- `.DeviceName` → deviceName
- `.RemoteEndPoint` → ipAddress (peut être IPv6-mapped, vérifier)
- `.PlayState.PlayMethod` → "DirectPlay", "Transcode", "DirectStream"
- `.PlayState.PositionTicks` → positionTicks
- `.PlayState.IsPaused` → isPaused
- `.PlayState.AudioStreamIndex` → audioStreamIndex
- `.PlayState.SubtitleStreamIndex` → subtitleStreamIndex
- `.TranscodingInfo` → transcodeFps (.Framerate), bitrate (.Bitrate), videoCodec (.VideoCodec), audioCodec (.AudioCodec)

#### Pour les codecs audio/subtitles depuis les MediaStreams :
```csharp
var streams = item.GetMediaStreams();
var audioIdx = session.PlayState?.AudioStreamIndex;
var subIdx = session.PlayState?.SubtitleStreamIndex;

// Audio
var audioStream = audioIdx.HasValue 
    ? streams?.FirstOrDefault(s => s.Index == audioIdx.Value && s.Type == MediaStreamType.Audio)
    : streams?.FirstOrDefault(s => s.Type == MediaStreamType.Audio && s.IsDefault);
// audioStream?.Language, audioStream?.Codec

// Subtitle
var subStream = subIdx.HasValue && subIdx.Value >= 0
    ? streams?.FirstOrDefault(s => s.Index == subIdx.Value && s.Type == MediaStreamType.Subtitle)
    : null;
// subStream?.Language, subStream?.Codec
```

#### Pour le collectionType :
```csharp
// Déterminer la bibliothèque parente
var libraryManager = // injecté via DI
var collectionFolder = libraryManager.GetCollectionFolders(item).FirstOrDefault();
string collectionType = collectionFolder?.CollectionType?.ToString()?.ToLower() ?? InferFromType(item);
string libraryName = collectionFolder?.Name;
```

#### Pour la liste des utilisateurs (Heartbeat) :
```csharp
var userManager = // injecté via DI
var users = userManager.Users.Select(u => new {
    jellyfinUserId = u.Id.ToString(),
    username = u.Username
}).ToList();
```

### Hooks Jellyfin à implémenter

Le plugin doit s'abonner aux événements suivants via l'injection de dépendances de Jellyfin :

```csharp
// Dans Plugin.cs ou via IServerEntryPoint
_sessionManager.PlaybackStart += OnPlaybackStart;
_sessionManager.PlaybackStopped += OnPlaybackStopped;
_sessionManager.PlaybackProgress += OnPlaybackProgress;
```

Ou via la méthode plus moderne avec `IEventConsumer<T>` :
```csharp
public class PlaybackStartNotifier : IEventConsumer<PlaybackStartEventArgs>
{
    public async Task OnEvent(PlaybackStartEventArgs e)
    {
        // Construire le payload et l'envoyer
    }
}
```

Pour les changements de bibliothèque :
```csharp
_libraryManager.ItemAdded += OnItemAdded;
_libraryManager.ItemUpdated += OnItemUpdated;
```

> **Attention** : Les événements `ItemAdded`/`ItemUpdated` peuvent se déclencher en rafale lors d'un scan de bibliothèque. Implémenter un **debounce/batch** : collecter les items pendant 30 secondes, puis envoyer un seul événement `LibraryChanged` avec tous les items accumulés.

### Service de Heartbeat

Utiliser un `IScheduledTask` ou un `IHostedService` / `BackgroundService` :
```csharp
public class HeartbeatService : IScheduledTask
{
    // Envoie un heartbeat toutes les N secondes
    // Le heartbeat inclut : pluginVersion, serverName (via IServerApplicationHost.FriendlyName), jellyfinVersion
    // ET la liste complète des utilisateurs Jellyfin (via IUserManager)
}
```

### Client HTTP (JellyTrackApiClient)

```csharp
public class JellyTrackApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    
    public async Task<bool> SendEventAsync(object eventPayload)
    {
        var config = Plugin.Instance.Configuration;
        if (string.IsNullOrEmpty(config.JellyTrackUrl) || string.IsNullOrEmpty(config.ApiKey))
            return false;
        
        var url = config.JellyTrackUrl.TrimEnd('/');
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(eventPayload),
            Encoding.UTF8,
            "application/json"
        );
        
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("JellyTrack API returned {StatusCode}", response.StatusCode);
        }
        return response.IsSuccessStatusCode;
    }
}
```

### Page de configuration HTML

Créer une page `Configuration/configPage.html` qui s'intègre dans l'admin Jellyfin :
- Champ URL JellyTrack (text input)
- Champ Clé API (password input avec bouton voir/masquer)
- Intervalle heartbeat (number input, défaut 60)
- Intervalle PlaybackProgress (number input, défaut 15)
- Toggle activer/désactiver
- Bouton "Tester la connexion" qui appelle l'API heartbeat et affiche le résultat
- Design cohérent avec l'admin Jellyfin (utiliser les classes CSS de Jellyfin)

### Gestion des erreurs

- **Retry** : Si JellyTrack est injoignable, ne pas bloquer Jellyfin. Logger l'erreur et continuer.
- **Queue** : Optionnellement, mettre en file d'attente les événements échoués pour les renvoyer plus tard (max 100 événements en mémoire).
- **Timeout** : 5 secondes pour chaque appel HTTP.
- **Logging** : Utiliser `ILogger<T>` pour tous les logs. Niveaux : Debug pour chaque événement envoyé, Warning pour les erreurs réseau, Error pour les erreurs critiques.

### Compatibilité Jellyfin

- **SDK Jellyfin** : Cibler la version 10.9.x (dernière stable)
- **NuGet** : `Jellyfin.Controller`, `Jellyfin.Model`
- **Target Framework** : `net8.0`
- Le plugin doit être compilable en un seul `.dll` installable dans le dossier plugins de Jellyfin

### Build & Packaging

Fournir :
1. Le fichier `.csproj` complet avec les bonnes références
2. Un `build.yaml` ou `meta.json` pour la soumission au repository de plugins
3. Un `README.md` avec les instructions d'installation et de configuration
4. Un `CONTEXT.md` détaillé documentant l'architecture du plugin

### À éviter

- Ne PAS utiliser de polling depuis le plugin (c'est l'ancien système qu'on remplace)
- Ne PAS stocker de données côté Jellyfin (tout est envoyé à JellyTrack)
- Ne PAS envoyer le contenu des images/médias, uniquement les métadonnées
- Ne PAS hard-coder d'URL ou de clé API — tout doit être configurable
- Ne PAS bloquer le thread principal de Jellyfin — tout doit être asynchrone

## Comment JellyTrack traite les événements

Voici ce que fait l'endpoint `/api/plugin/events` côté JellyTrack pour chaque type d'événement — le plugin doit envoyer les données dans le bon format pour que ce traitement fonctionne :

### Heartbeat
- Met à jour `pluginLastSeen`, `pluginVersion`, `pluginServerName` dans GlobalSettings
- **Synchronise les utilisateurs** : Pour chaque utilisateur dans le tableau `users`, fait un upsert dans la table User (jellyfinUserId + username)

### PlaybackStart
- Upsert User (crée/met à jour) + Upsert Media (crée/met à jour avec tous les champs enrichis)
- Vérifie les bibliothèques exclues (par collectionType)
- GeoIP lookup sur l'adresse IP
- **Déduplication** : Si une session ouverte existe déjà pour ce user+media → ne crée pas de doublon. Si une session fermée récemment (< 1h) existe → la rouvre (merge window)
- Crée un ActiveStream (session temps réel) + cache Redis
- Envoie une notification Discord si configurée

### PlaybackStop
- Ferme la PlaybackHistory avec le temps regardé (min entre wall-clock et positionTicks)
- Enregistre un TelemetryEvent "stop" avec la position
- Nettoie les clés Redis de télémétrie
- Supprime l'ActiveStream

### PlaybackProgress
- Détecte les transitions pause/play → incrémente `pauseCount`
- Détecte les changements de piste audio → incrémente `audioChanges`
- Détecte les changements de sous-titres → incrémente `subtitleChanges`
- Enregistre des TelemetryEvents pour chaque changement
- Met à jour la position dans ActiveStream + Redis

### LibraryChanged
- Upsert en masse des médias (créer ou mettre à jour)
- Tous les champs sont enrichis par le plugin : genres, résolution, durée, etc.

## Résumé des livrables

1. **`CONTEXT.md`** — Documentation technique complète du plugin
2. **Code source complet** — Tous les fichiers listés dans la structure du projet
3. **`README.md`** — Guide d'installation utilisateur
4. **Tests manuels recommandés** — Liste des scénarios à vérifier
