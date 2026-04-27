#+ NOTE: Fichier de reference unique pour les agents IA travaillant sur le plugin.

![JellyTrack Plugin Logo](../../assets/banner.png)

# JellyTrack Plugin - Instructions pour agents IA (v1.4.0)

IMPORTANT (pour agents IA) - lire entierement ce document avant de proposer des modifications.

- Ne pas halluciner formats de payload, champs d'evenements ou cles i18n.
  - Sources canoniques: `JellyTrack.Plugin/Models/*.cs`, `JellyTrack.Plugin/Notifiers/*`, `JellyTrack.Plugin/Services/*`.
- Ne pas inventer le contrat serveur.
  - Source canonique cote app: `JellyTrack/src/app/api/plugin/events/route.ts`.
- Respecter les conventions endpoint, schema event et plugin key du projet parent.
- Methode d'installation recommandee pour les utilisateurs finaux: catalogue Jellyfin via `manifest.json`.
- Ne pas faire de `commit`, `push`, creation de branche ou `merge` sans demande explicite utilisateur.

## 1. Vue d'ensemble

Le plugin JellyTrack (C#) est l'emetteur d'evenements Jellyfin vers l'application JellyTrack (Next.js).

Pipeline principal:

1. Capture des evenements locaux (start/progress/stop/library + heartbeat).
2. Serialisation JSON standard (`System.Text.Json`).
3. POST HTTP vers l'endpoint configure (par defaut `/api/plugin/events`).
4. Retry local avec queue memoire limitee.

Objectif: flux fiable, idempotent et peu intrusif.

## 2. Stack technique (canonique)

- Langage: C# (`net9.0`)
- Pattern: `IEventConsumer<T>`, `IScheduledTask`, `IHostedService`
- Serialisation: `System.Text.Json`
- Reseau: `HttpClient` via `IHttpClientFactory`
- API Jellyfin: `Jellyfin.Controller` + `Jellyfin.Model` (10.11.x)

## 3. Contrat de Compatibilite avec JellyTrack App

Source de reference serveur: `JellyTrack/src/app/api/plugin/events/route.ts`

### 3.1 Types d'evenements acceptes

- `Heartbeat`
- `PlaybackStart`
- `PlaybackProgress`
- `PlaybackStop`
- `LibraryChanged`

### 3.2 Version de schema obligatoire

- `eventSchemaVersion` doit etre present sur tous les payloads.
- Version supportee cote serveur v1.4.0: `2` (strict).
- Source plugin: `JellyTrack.Plugin/Models/PluginEvent.cs`.

### 3.3 Auth plugin key (hash-at-rest cote app)

- Le plugin envoie la cle brute (jamais hash) uniquement en transport HTTP.
- Headers supportes et recommandes:
  - `Authorization: Bearer <pluginKey>`
  - `X-Api-Key: <pluginKey>`
- Source plugin: `JellyTrack.Plugin/Services/JellyTrackApiClient.cs`.
- Cote serveur, la cle est comparee au hash scrypt stocke (`pluginApiKey`) via timing-safe compare.

Important:
- Ne jamais tenter de reproduire le hash scrypt dans le plugin.
- Ne jamais stocker la cle dans d'autres fichiers que la config plugin Jellyfin.

### 3.4 Cles scopees multi-serveur

- Format possible fourni par l'app: `jts3.<serverIdBase64url>.<rawKey>`.
- Le plugin doit transmettre ce token tel quel sans le parser/casser.
- Le serveur extrait la partie scopee et verifie la coherence avec le `serverId` du payload.

## 4. Politique Heartbeat (Performance reseau)

Source: `JellyTrack.Plugin/Services/HeartbeatService.cs` + `JellyTrack.Plugin/PluginConfiguration.cs`

- Premier heartbeat envoye immediatement au demarrage (signal de presence rapide).
- Intervalle par defaut: `600` secondes (10 minutes).
- Intervalle minimum applique: `300` secondes (5 minutes).
- Si config invalide/<=0: fallback automatique sur le defaut 600s.

Objectif:
- reduire fortement le bruit reseau tout en gardant un signal de sante periodique.

## 5. Robustesse & Resilience

Source: `JellyTrack.Plugin/Services/JellyTrackApiClient.cs`

- Timeout HTTP court (5s) pour eviter de bloquer le serveur Jellyfin.
- Queue memoire de retry bornee (`MaxQueueSize = 100`).
- Flush de queue avant envoi du nouvel evenement.
- En echec reseau/API: requeue et retry progressif lors des envois suivants.

## 6. Structure de travail (vue utile)

- `JellyTrack.Plugin/Plugin.cs`: definition plugin + page config
- `JellyTrack.Plugin/PluginConfiguration.cs`: options persistantes
- `JellyTrack.Plugin/PluginServiceRegistrator.cs`: DI/registre services
- `JellyTrack.Plugin/Api/JellyTrackController.cs`: endpoints admin plugin
- `JellyTrack.Plugin/Configuration/configPage.html`: UI de configuration
- `JellyTrack.Plugin/Models/*.cs`: contrat evenementiel
- `JellyTrack.Plugin/Notifiers/*.cs`: capteurs playback
- `JellyTrack.Plugin/Services/JellyTrackApiClient.cs`: client HTTP vers app
- `JellyTrack.Plugin/Services/HeartbeatService.cs`: heartbeat periodique
- `JellyTrack.Plugin/Services/LibraryChangeNotifier.cs`: debounce/batch bibliotheque

## 7. Internationalisation plugin

- Fichiers: `Localization/*.json`.
- La page `configPage.html` charge les traductions via endpoint plugin.
- Toute nouvelle cle UI doit etre ajoutee dans toutes les locales plugin.

## 8. Regles Qualite Zero Dette Technique

Avant finalisation:

1. Verifier compatibilite contrat avec `JellyTrack/src/app/api/plugin/events/route.ts`.
2. Verifier `eventSchemaVersion` sur tout nouvel evenement.
3. Verifier que les headers auth restent compatibles (`Authorization` + `X-Api-Key`).
4. Executer `dotnet build` dans `JellyTrack.Plugin/JellyTrack.Plugin`.
5. Si contrat modifie: mettre a jour en parallele
   - `JellyTrack/.claude/rules/instructions.md`
   - `JellyTrack.Plugin/.claude/rules/instructions.md`
6. Verifier qu'aucun secret reel n'est ajoute au repo (manifest/config/doc).

## 9. Commandes de Reference

- Build plugin: `dotnet build JellyTrack.Plugin/JellyTrack.Plugin/JellyTrack.Plugin.csproj`
- Build solution: `dotnet build JellyTrack.Plugin/plugin-jellytrack.sln`
- Mise a jour manifeste: `python scripts/update_manifest.py`

## 10. Checklist Anti-Hallucination

Toujours verifier avant proposition:

- `JellyTrack.Plugin/Models/*.cs`
- `JellyTrack.Plugin/Services/JellyTrackApiClient.cs`
- `JellyTrack.Plugin/Services/HeartbeatService.cs`
- `JellyTrack.Plugin/Configuration/configPage.html`
- `JellyTrack/src/app/api/plugin/events/route.ts`
- `JellyTrack/src/lib/pluginKeyManager.ts`
- `JellyTrack/src/lib/pluginServerKey.ts`

Si un doute persiste: lire le fichier, ne pas deviner.

---

Ce document est la reference agents IA pour JellyTrack Plugin v1.4.0.
Toute evolution du contrat (payload, auth, schema version) doit mettre a jour ce document dans la meme PR.