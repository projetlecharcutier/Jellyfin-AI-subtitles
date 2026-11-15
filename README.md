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

## Déclenchement manuel (API, admin requis)

- `POST /AISubtitles/Scan/Recent` : scan des derniers médias ajoutés (limite configurable, 10 par défaut).
- `POST /AISubtitles/Scan/Item/{itemId}` : traduction IA pour un film ou un épisode précis.
- `POST /AISubtitles/Scan/Series/{seriesId}` : traduction IA pour une série entière (plafonnée à N épisodes configurables).

Un bouton « Lancer un scan des derniers médias maintenant » est aussi disponible sur la page de configuration du plugin.

## Règles appliquées

- Si plusieurs sous-titres existent, la source choisie est celle en **anglais** (à défaut le premier sous-titre externe, puis le premier trouvé).
- Les sous-titres embarqués (pistes internes du conteneur) sont extraits via ffmpeg si aucun fichier externe n'est disponible.
- Le fichier généré est déposé dans le dossier du média et nommé `<nom du média>.<langue>-AI.srt` (ex. `Dune.2021.french-AI.srt`), en SRT UTF-8.
- Si le fichier `-AI` existe déjà pour une langue, il n'est pas écrasé.
- Après génération, les métadonnées du média sont rafraîchies pour que Jellyfin voie le nouveau sous-titre.

## Build

```bash
dotnet build Jellyfin.Plugin.AISubtitles.slnx
```

Le manifeste `build.yaml` permet de packager le plugin pour le dépôt de plugins Jellyfin.
