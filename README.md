
![JellyTrack Plugin Banner](assets/banner.png)

# JellyTrack Plugin pour Jellyfin

Plugin pour Jellyfin qui envoie les événements de lecture et les métadonnées vers **JellyTrack** pour l'analyse et les statistiques.

## Important — dépendance au serveur JellyTrack

- Le plugin envoie les événements vers une instance JellyTrack : assurez-vous d'avoir une instance JellyTrack accessible (URL + clé API) avant d'activer le plugin. Sans serveur JellyTrack, le plugin n'enverra pas d'événements et n'aura pas d'effet visible.
- **Installation recommandée** : via le dépôt Jellyfin (voir ci‑dessous). Configurez l'URL JellyTrack dans la page de configuration du plugin et testez la connexion avant de laisser le plugin en production.

## Installation (RECOMMANDÉE : dépôt Jellyfin)

Privilégiez l'installation via le dépôt de plugins Jellyfin — c'est la méthode la plus simple et la plus sûre pour maintenir le plugin à jour.

1. Dans Jellyfin → Tableau de bord → Plugins → Dépôts
2. Cliquer sur `+` (Ajouter)
3. Remplir :
   - **Nom** : JellyTrack
   - **URL** : `https://raw.githubusercontent.com/maelmoreau21/JellyTrack.Plugin/main/manifest.json`
4. Enregistrer → Ouvrir le Catalogue → Rechercher **JellyTrack** → Installer
5. Redémarrer Jellyfin

Pourquoi ? Le dépôt permet d'installer et mettre à jour le plugin directement depuis l'interface Jellyfin sans copier manuellement de DLL.

Cette méthode est la méthode principale supportée pour la production.

## Installation manuelle (optionnelle)

1. Télécharger `JellyTrack.Plugin.dll` depuis les Releases
2. Créer le dossier `JellyTrack` dans le répertoire plugins de Jellyfin :
   - Linux : `/var/lib/jellyfin/plugins/JellyTrack/`
   - Windows : `C:\ProgramData\Jellyfin\Server\plugins\JellyTrack\`
   - Docker : `/config/plugins/JellyTrack/`
3. Copier `JellyTrack.Plugin.dll` dans ce dossier
4. Redémarrer Jellyfin

Utilisez cette méthode uniquement pour le debug local ou en environnement isolé.

## Configuration

Dans Jellyfin → Tableau de bord → Plugins → JellyTrack :

- **URL JellyTrack** : ex. `http://192.168.1.100:3000` (si vous entrez uniquement l'hôte, le plugin poste sur `/api/plugin/events`)
- **Clé API** : `jt_xxxxxxxxxxxx` (depuis JellyTrack)
- **Intervalle Heartbeat (s)** : 600 (par défaut, minimum 300)
- **Intervalle Progress (s)** : 15 (par défaut)
- **Activer / Désactiver** : bascule globale

Cliquer sur **Tester la connexion**, puis **Enregistrer**.

## Vérifications rapides

- Démarrer une lecture → vérifier la réception de `PlaybackStart` sur JellyTrack
- Pendant la lecture → vérifier `PlaybackProgress` (toutes les 15s par défaut)
- Arrêter la lecture → vérifier `PlaybackStop`
- Attendre le heartbeat → vérifier envoi périodique

## Compilation (développeurs)

```bash
git clone https://github.com/maelmoreau21/JellyTrack.Plugin.git
cd JellyTrack.Plugin/JellyTrack.Plugin
dotnet build -c Release
# Le binaire sera dans bin/Release/net9.0/JellyTrack.Plugin.dll
```

## Dépannage

- Plugin absent dans le catalogue → vérifier l'URL du manifeste et la connectivité réseau depuis le serveur Jellyfin
- Aucun événement reçu → vérifier la configuration (URL, clé), les logs Jellyfin et JellyTrack
- Message `Unauthorized` juste après installation → ce n'est pas un échec d'installation du dépôt. Cela signifie que le plugin essaie d'envoyer un heartbeat avec une clé API invalide/ancienne. Ouvrir la configuration du plugin, corriger URL + clé API, ou désactiver le plugin tant que JellyTrack n'est pas prêt.
- Si vous utilisez une URL contenant un chemin personnalisé, assurez-vous que l'endpoint côté JellyTrack accepte `/api/plugin/events` ou `/api/webhook/jellyfin` selon votre configuration

## Licence

MIT

## Internationalisation / Langues

- Le plugin détecte par défaut la langue d'interface du serveur Jellyfin et l'envoie dans le `Heartbeat` vers JellyTrack. Cela permet à JellyTrack d'utiliser par défaut la même langue que votre serveur Jellyfin.
- Vous pouvez surcharger ce comportement dans la configuration du plugin (option **Langue préférée**) : laissez vide pour utiliser la langue de Jellyfin, ou renseignez un code de langue ISO (ex: `en`, `fr`, `pt-BR`).

## Dépôts liés

- Application JellyTrack : https://github.com/maelmoreau21/JellyTrack
- Plugin JellyTrack pour Jellyfin : https://github.com/maelmoreau21/JellyTrack.Plugin
