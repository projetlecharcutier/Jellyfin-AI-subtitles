# Jellyfin AI Subtitles

Plugin Jellyfin qui scanne les sous-titres existants de la bibliothèque et génère, via l'API Mistral AI, des sous-titres traduits dans une ou plusieurs langues choisies.

## Fonctionnement

- Si un média ne possède **aucun** sous-titre, le plugin ne fait **rien** pour lui (aucun fichier créé, modifié ou supprimé).
- Si plusieurs sous-titres existent, la source est celle en **anglais** (à défaut : premier sous-titre externe, puis premier trouvé).
- Sinon, un fichier par langue cible est déposé dans le dossier du média, nommé `<nom du média>.<langue>-AI.srt` (ex. `Dune.2021.french-AI.srt`), en SRT UTF-8.
- Le scan automatique ne traite que les **10 derniers médias ajoutés** (limite configurable), pour ne pas surcharger le serveur ni l'API IA.
- Il est possible de déclencher la traduction sur un film précis ou sur une série entière (voir API ci-dessous).

## Garanties de sécurité

- **Aucun fichier existant n'est jamais écrasé ni supprimé** : le plugin écrit uniquement avec `FileMode.CreateNew` (échec si le fichier existe) et ignore toute langue dont le fichier `-AI` existe déjà. Le seul fichier jamais supprimé est un fichier temporaire créé par le plugin lui-même dans le dossier temporaire du système.
- Si la source (fichier externe ou piste embarquée) est illisible, le média est ignoré sans action.

## Déploiement (Windows, aucune installation complémentaire)

Le plugin ne dépend que du .NET 9 embarqué par Jellyfin 10.11 et de l'API Jellyfin : **aucune installation supplémentaire n'est requise** (ffmpeg, s'il est utilisé pour extraire une piste embarquée, est celui embarqué par Jellyfin lui-même).

1. Compiler le plugin :
   ```powershell
   dotnet build -c Release Jellyfin.Plugin.AISubtitles.slnx
   ```
   Le fichier `Jellyfin.Plugin.AISubtitles\bin\Release\net9.0\Jellyfin.Plugin.AISubtitles.dll` est produit.
2. Installer dans le dossier des plugins de Jellyfin, **dans un sous-dossier de version `1.0.0.0` contenant le DLL et le `meta.xml`** (obligatoire depuis Jellyfin 10.9 : sans sous-dossier de version le plugin apparaît « Deleted », sans `meta.xml` il apparaît « NotSupported » dans le dashboard) :
   ```powershell
   New-Item -ItemType Directory -Force "$env:ProgramData\Jellyfin\Server\plugins\Jellyfin.Plugin.AISubtitles\1.0.0.0"
   Copy-Item "Jellyfin.Plugin.AISubtitles\\bin\\Release\\net9.0\\Jellyfin.Plugin.AISubtitles.dll" "$env:ProgramData\Jellyfin\Server\plugins\Jellyfin.Plugin.AISubtitles\1.0.0.0\"
   Copy-Item "dist\Jellyfin.Plugin.AISubtitles\1.0.0.0\meta.xml" "$env:ProgramData\Jellyfin\Server\plugins\Jellyfin.Plugin.AISubtitles\1.0.0.0\"
   ```
   Résultat attendu :
   ```
   ...\plugins\Jellyfin.Plugin.AISubtitles\1.0.0.0\Jellyfin.Plugin.AISubtitles.dll
   ...\plugins\Jellyfin.Plugin.AISubtitles\1.0.0.0\meta.xml
   ```
   Ou plus simplement : extraire `dist\Jellyfin.Plugin.AISubtitles.zip` directement dans le dossier `plugins\` (le zip contient déjà `Jellyfin.Plugin.AISubtitles\1.0.0.0\` avec DLL + meta.xml).
   Sur une installation avec dossier de données personnalisé, remplacer `$env:ProgramData\Jellyfin\Server` par ton dossier de données Jellyfin (ex. `E:\Jellyfin\Server`).
3. Redémarrer Jellyfin.
4. Ouvrir **Tableau de bord → Avancé → Plugins → AI Subtitles** et renseigner la clé API Mistral, le modèle et les langues cibles.

Alternative : une fois le plugin publié dans le dépôt officiel des plugins Jellyfin (via le `build.yaml`), l'installation se fait directement depuis **Tableau de bord → Plugins → Dépôts**.

## Modèle conseillé (meilleur rapport qualité/coût)

- **`mistral-small-latest`** (recommandé, et configuré par défaut) : excellent rapport qualité/prix pour de la traduction de sous-titres en volume.
- `mistral-medium-latest` : si tu veux un cran de qualité au-dessus pour un coût encore raisonnable.
- `mistral-large-latest` : qualité maximale, coût plus élevé — à réserver aux contenus exigeants.

## Prompt utilisé

Le plugin envoie ce prompt système au modèle (les lignes à traduire sont numérotées dans le message utilisateur, et le modèle doit répondre uniquement en JSON `{"lines":[...]}` avec exactement le même nombre de lignes) :

> You are an expert audiovisual subtitle translator. Translate each numbered line into {langue}. Rules: produce exactly one output line per input line, in the same order; preserve the meaning, tone and register of the dialogue; keep proper nouns, brands and onomatopoeia unchanged unless they are commonly translated; never merge, split, add or remove lines; never add explanations, notes or numbering; write natural, idiomatic {langue} suited to subtitles (concise, readable). Reply ONLY with a JSON object of the form {"lines":[...]} containing exactly N translated strings, in the same order as the input.

## Configuration

Dans le dashboard Jellyfin (Avancé → Plugins → AI Subtitles) :

- **Clé API Mistral** : votre clé d'API Mistral (obtenue sur [console.mistral.ai](https://console.mistral.ai)).
- **Modèle Mistral** : voir recommandation ci-dessus.
- **Langues cibles** : cochez les langues pour lesquelles générer une traduction.
- **Scan automatique** : activation et intervalle (en heures), limité aux derniers médias ajoutés.
- Bouton **« Lancer un scan des derniers médias maintenant »** pour un scan manuel immédiat.

## Déclenchement manuel (API, admin requis)

- `POST /AISubtitles/Scan/Recent` : scan des derniers médias ajoutés (limite configurable, 10 par défaut).
- `POST /AISubtitles/Scan/Item/{itemId}` : traduction IA pour un film ou un épisode précis.
- `POST /AISubtitles/Scan/Series/{seriesId}` : traduction IA pour une série entière (plafonnée à N épisodes configurables).

## Build

```bash
dotnet build Jellyfin.Plugin.AISubtitles.slnx
```

Le manifeste `build.yaml` permet de packager le plugin pour le dépôt de plugins Jellyfin.
