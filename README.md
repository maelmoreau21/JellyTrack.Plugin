<p align="center">
  <img src="https://raw.githubusercontent.com/maelmoreau21/JellyTrack/main/public/logo.svg" width="128" height="128" alt="JellyTrack Logo">
</p>

<h1 align="center">JellyTrack Plugin</h1>

<p align="center">
  <img src="https://img.shields.io/github/v/release/maelmoreau21/JellyTrack.Plugin" alt="GitHub Release">
  <img src="https://img.shields.io/github/license/maelmoreau21/JellyTrack.Plugin" alt="License">
</p>

<p align="center">
  <strong>Plugin Jellyfin pour JellyTrack : capture et envoie les événements de lecture et métadonnées en temps réel vers votre serveur d'analytics.</strong>
</p>

---

> [!IMPORTANT]
> ### 🚨 SERVEUR JELLYTRACK REQUIS
> Ce plugin ne fonctionne que s'il est connecté à une instance active de **JellyTrack**. Sans serveur, le plugin n'aura aucun effet visible.
> 
> [👉 Déployer le serveur JellyTrack](https://github.com/maelmoreau21/JellyTrack)

---

## 🔌 Installation (Méthode Recommandée : Dépôt Jellyfin)

Privilégiez l'installation via le dépôt officiel pour bénéficier des mises à jour automatiques directement depuis votre interface Jellyfin.

### 1. Ajouter le dépôt
1. Dans Jellyfin : **Tableau de bord** > **Plugins** > **Dépôts**.
2. Cliquez sur le bouton `+` (Ajouter).
3. Remplissez les informations suivantes :
   - **Nom** : `JellyTrack`
   - **URL** : `https://raw.githubusercontent.com/maelmoreau21/JellyTrack.Plugin/main/manifest.json`

### 2. Installation
1. Allez dans l'onglet **Catalogue**.
2. Recherchez **JellyTrack** et installez-le.
3. **Redémarrez Jellyfin** pour activer le plugin.

---

## ⚙️ Configuration

Une fois installé, rendez-vous dans **Tableau de bord** > **Plugins** > **JellyTrack** pour configurer la connexion :

- **URL JellyTrack** : L'adresse de votre serveur (ex: `http://192.168.1.100:3000`).
- **Clé API** : La clé générée dans l'interface de JellyTrack (format `jt_xxxxxxxxxxxx`).
- **Intervalle Heartbeat** : Fréquence de vérification de santé (défaut: 600s).

> [!TIP]
> Utilisez le bouton **Tester la connexion** pour vérifier que le plugin communique correctement avec votre serveur avant d'enregistrer.

---

## 🛠️ Installation Manuelle (Optionnelle)

Si vous ne pouvez pas utiliser le dépôt :
1. Téléchargez le fichier `JellyTrack.Plugin.dll` depuis les [Releases](https://github.com/maelmoreau21/JellyTrack.Plugin/releases).
2. Créez un dossier `JellyTrack` dans votre répertoire `plugins` Jellyfin.
3. Copiez le fichier `.dll` dedans et redémarrez Jellyfin.

---

## 📄 Licence

Distribué sous licence **MIT**.
