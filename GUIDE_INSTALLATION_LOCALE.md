# Installation locale du plugin JellyTrack

Ce dépôt contient déjà une build corrigée du plugin ainsi qu'un zip prêt à installer.

Version locale préparée : `1.0.3.0`

## Fichiers prêts à utiliser

- DLL Release : `C:\Users\Mael\Documents\GitHub\JellyTrack.Plugin\artifacts\JellyTrack.Plugin.dll`
- ZIP prêt à installer/manipuler : `C:\Users\Mael\Documents\GitHub\JellyTrack.Plugin\artifacts\JellyTrack.Plugin.zip`
- ZIP versionné release : `C:\Users\Mael\Documents\GitHub\JellyTrack.Plugin\artifacts\JellyTrack.Plugin-1.0.3.0.zip`

## Ce qui a été corrigé dans cette build

- Le bouton **Tester la connexion** ne passe plus par un `fetch` navigateur vers JellyTrack.
- Le test passe maintenant par un endpoint serveur du plugin, donc plus de faux `Failed to fetch` liés au navigateur.
- Les erreurs HTTP de JellyTrack remontent avec plus de détails.
- Un **heartbeat immédiat** est envoyé juste après l'enregistrement de la configuration du plugin.

## Installation manuelle dans Jellyfin

### Windows

1. Fermez ou arrêtez le service Jellyfin.
2. Allez dans le dossier des plugins Jellyfin :
   `C:\ProgramData\Jellyfin\Server\plugins\JellyTrack\`
3. Remplacez l'ancienne DLL par celle-ci :
   `C:\Users\Mael\Documents\GitHub\JellyTrack.Plugin\artifacts\JellyTrack.Plugin.dll`
4. Redémarrez Jellyfin.

### Docker

1. Ouvrez le volume/dossier plugins monté dans votre conteneur Jellyfin.
2. Placez la DLL dans :
   `/config/plugins/JellyTrack/JellyTrack.Plugin.dll`
3. Redémarrez le conteneur Jellyfin.

## Configuration à mettre dans le plugin

Dans Jellyfin, allez dans :
`Tableau de bord -> Plugins -> JellyTrack`

Renseignez :

- **URL de JellyTrack** : l'URL complète de l'endpoint plugin
  Exemple : `http://IP_DE_TA_MACHINE:3000/api/plugin/events`
- **Clé API** : la clé affichée dans JellyTrack > Settings > Jellyfin Plugin
- **Heartbeat** : `60`
- **PlaybackProgress** : `15`

## Ordre recommandé

1. Ouvrez JellyTrack.
2. Copiez l'URL JellyTrack avec le bouton dédié.
3. Copiez la clé API.
4. Collez les deux dans le plugin Jellyfin.
5. Cliquez sur **Enregistrer**.
6. Le plugin envoie maintenant un heartbeat immédiatement après l'enregistrement.
7. Cliquez ensuite sur **Tester la connexion** si vous voulez une vérification explicite.

## Si le test échoue encore

Regardez le message affiché dans la page du plugin.

Cas typiques :

- `401` : mauvaise clé API
- `404` : mauvaise URL
- `timeout` / `No such host` / `connection refused` : problème réseau ou mauvaise IP/port

## Important pour Docker

Si Jellyfin tourne en Docker, n'utilisez pas `localhost` pour joindre JellyTrack sauf si JellyTrack tourne dans le même conteneur.

Utilisez plutôt :

- l'IP locale de la machine
- ou `host.docker.internal` si votre environnement Docker le supporte

## Vérification finale

Après installation et configuration :

1. Sauvegardez la config du plugin.
2. Vérifiez dans JellyTrack > Settings que le statut passe à **Connecté** dans les secondes qui suivent.
3. Lancez une lecture dans Jellyfin.
4. Vérifiez que JellyTrack reçoit `PlaybackStart`, puis `PlaybackProgress`, puis `PlaybackStop`.