# JellyTrack Plugin pour Jellyfin

Plugin Jellyfin qui envoie les événements de lecture et les métadonnées vers [JellyTrack](https://github.com/jellytrack) pour l'analyse et les statistiques.

## Fonctionnalités

- **PlaybackStart** : Envoie les métadonnées complètes du média et de la session à chaque début de lecture
- **PlaybackProgress** : Mise à jour périodique de la position de lecture (défaut: 15s)
- **PlaybackStop** : Notification de fin de lecture avec position finale
- **Heartbeat** : Signal de vie périodique (défaut: 60s) avec synchronisation des utilisateurs
- **LibraryChanged** : Notification d'ajout/modification de médias avec debounce (30s)

## Installation

### Méthode 1 : Installation manuelle

1. Téléchargez le fichier `JellyTrack.Plugin.dll` depuis la page [Releases](https://github.com/jellytrack/plugin-jellytrack/releases)
2. Créez le dossier `JellyTrack` dans le répertoire des plugins de Jellyfin :
   - **Linux** : `/var/lib/jellyfin/plugins/JellyTrack/`
   - **Windows** : `C:\ProgramData\Jellyfin\Server\plugins\JellyTrack\`
   - **Docker** : `/config/plugins/JellyTrack/`
3. Copiez `JellyTrack.Plugin.dll` dans ce dossier
4. Redémarrez Jellyfin

### Méthode 2 : Ajouter le dépôt JellyTrack automatiquement

1. Dans Jellyfin, allez dans **Tableau de bord** > **Plugins** > **Dépôts**.
2. Cliquez sur le bouton `+` pour ajouter un nouveau dépôt.
3. Entrez les informations suivantes :
   - **Nom** : JellyTrack
   - **URL du dépôt** : `https://maelmoreau21.github.io/JellyTrack.Plugin/manifest.json`
4. Allez dans le catalogue des plugins, cherchez "JellyTrack" et installez-le.
5. Redémarrez Jellyfin.

## Configuration

1. Dans l'admin Jellyfin, allez dans **Tableau de bord → Plugins → JellyTrack**
2. Configurez :
   - **URL de JellyTrack** : L'endpoint API de votre instance JellyTrack (ex: `http://192.168.1.100:3000/api/plugin/events`)
   - **Clé API** : La clé générée dans JellyTrack (format: `jt_xxxxxxxxxxxx`)
   - **Intervalle Heartbeat** : Fréquence des heartbeats en secondes (défaut: 60)
   - **Intervalle Progress** : Fréquence des mises à jour de progression en secondes (défaut: 15)
   - **Activer/Désactiver** : Toggle global du plugin
3. Cliquez sur **Tester la connexion** pour vérifier
4. **Enregistrez** la configuration

## Compilation depuis les sources

```bash
# Cloner le repo
git clone https://github.com/jellytrack/plugin-jellytrack.git
cd plugin-jellytrack/JellyTrack.Plugin

# Compiler
dotnet build -c Release

# Le fichier compilé sera dans bin/Release/net8.0/JellyTrack.Plugin.dll
```

## Prérequis

- Jellyfin **10.10.x** ou supérieur
- .NET 8.0 SDK (pour la compilation)

## Tests manuels recommandés

| Scénario | Vérification |
|----------|-------------|
| Démarrer la lecture d'un film | Vérifier que JellyTrack reçoit un `PlaybackStart` avec les métadonnées complètes |
| Lire un épisode de série | Vérifier `seriesName`, `seasonName` dans l'événement |
| Lire une chanson | Vérifier `albumName`, `albumArtist`, `artist` dans l'événement |
| Attendre 15s pendant la lecture | Vérifier la réception de `PlaybackProgress` |
| Mettre en pause puis reprendre | Vérifier que `isPaused` change dans les progress |
| Changer de piste audio | Vérifier que `audioStreamIndex` change |
| Activer des sous-titres | Vérifier que `subtitleStreamIndex` change |
| Arrêter la lecture | Vérifier la réception de `PlaybackStop` avec `positionTicks` |
| Attendre le heartbeat (60s) | Vérifier que JellyTrack reçoit un `Heartbeat` avec la liste des utilisateurs |
| Ajouter un nouveau média à la bibliothèque | Vérifier la réception de `LibraryChanged` (après ~30s de debounce) |
| Couper la connexion réseau avec JellyTrack | Vérifier que Jellyfin continue de fonctionner normalement |
| Rétablir la connexion | Vérifier que les événements en queue sont renvoyés |
| Désactiver le plugin | Vérifier qu'aucun événement n'est envoyé |
| Laisser l'URL vide | Vérifier qu'aucune erreur ne bloque Jellyfin |

## Architecture

Voir [CONTEXT.md](JellyTrack.Plugin/CONTEXT.md) pour la documentation technique détaillée.

## Licence

MIT
