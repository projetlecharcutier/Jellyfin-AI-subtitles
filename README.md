# Jellyfin AI Subtitles

Plugin Jellyfin qui scanne les sous-titres existants de la bibliothèque et génère, via l'API Mistral AI, des sous-titres traduits dans une ou plusieurs langues choisies.

## Fonctionnement

- Si un média ne possède **aucun** sous-titre, le plugin ne fait rien pour lui.
- Sinon, un fichier de sous-titres par langue cible est déposé dans le dossier du média, nommé `<langue>-AI` (ex. `fre-AI`).
- Le scan automatique ne traite que les **10 derniers médias ajoutés** (limite configurable), pour ne pas surcharger le serveur ni l'API IA.
- Il est possible de déclencher la traduction IA sur un film précis ou sur une série entière.

## Configuration

Dans le dashboard Jellyfin (Avancé → Plugins → AI Subtitles) :

- **Clé API Mistral** : votre clé d'API Mistral.
- **Modèle Mistral** : le modèle à utiliser (ex. `mistral-large-latest`).
- **Langues cibles** : cochez les langues pour lesquelles générer une traduction.
- **Scan automatique** : activation et intervalle (en heures).

## Build

```bash
dotnet build Jellyfin.Plugin.AISubtitles.slnx
```

Le manifeste `build.yaml` permet de packager le plugin pour le dépôt de plugins Jellyfin.
