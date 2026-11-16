# Jellyfin AI Subtitles

Plugin Jellyfin qui scanne les sous-titres existants de la bibliothèque et génère, via l'API Mistral AI, des sous-titres traduits dans une ou plusieurs langues choisies.

## Fonctionnement

- Si un média ne possède **aucun** sous-titre, le plugin ne fait **rien** pour lui (aucun fichier créé, modifié ou supprimé).
- Si plusieurs sous-titres existent, la source est celle en **anglais** (à défaut : premier sous-titre externe, puis premier trouvé).
- Sinon, un fichier par langue cible est déposé dans le dossier du média, nommé `<nom du média>.<langue>-AI.srt` (ex. `Dune.2021.french-AI.srt`), en SRT UTF-8.
- Le scan automatique parcourt les **500 derniers médias ajoutés** et s'arrête dès que **5 médias ayant un sous-titre à traduire** ont été traités (limite configurable, défaut 5), pour ne pas surcharger le serveur ni l'API IA. Les 20 derniers médias traités sont affichés sur la page de configuration du plugin.
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
2. Installer dans le dossier des plugins de Jellyfin. Structure attendue par Jellyfin 10.11 (vérifiée dans le source `PluginManager`) : **le DLL et le `meta.json` (JSON) vont tous deux à la racine du dossier plugin** ; les entrées `assemblies` du `meta.json` sont résolues relativement à cette racine :
   ```
   ...\plugins\Jellyfin.Plugin.AISubtitles\meta.json
   ...\plugins\Jellyfin.Plugin.AISubtitles\Jellyfin.Plugin.AISubtitles.dll
   ```
   ```powershell
   New-Item -ItemType Directory -Force "$env:ProgramData\Jellyfin\Server\plugins\Jellyfin.Plugin.AISubtitles"
   Copy-Item "dist\Jellyfin.Plugin.AISubtitles\meta.json" "$env:ProgramData\Jellyfin\Server\plugins\Jellyfin.Plugin.AISubtitles\"
   Copy-Item "dist\Jellyfin.Plugin.AISubtitles\Jellyfin.Plugin.AISubtitles.dll" "$env:ProgramData\Jellyfin\Server\plugins\Jellyfin.Plugin.AISubtitles\"
   ```
   Ou plus simplement : extraire `dist\Jellyfin.Plugin.AISubtitles.zip` directement dans le dossier `plugins\`.
   Sur une installation avec dossier de données personnalisé, remplacer `$env:ProgramData\Jellyfin\Server` par ton dossier de données Jellyfin (ex. `E:\Jellyfin\Server`).
   Important : builder avec les packages `Jellyfin.Controller`/`Jellyfin.Model` **10.11.0** (déjà le cas dans le csproj) pour que le DLL charge sur tout serveur 10.11.x (un build 10.11.5 sur un serveur 10.11.2 donne « NotSupported »).
3. Redémarrer Jellyfin.
4. Ouvrir **Tableau de bord → Avancé → Plugins → AI Subtitles** et renseigner la clé API Mistral, le modèle et les langues cibles.

Alternative : une fois le plugin publié dans le dépôt officiel des plugins Jellyfin (via le `build.yaml`), l'installation se fait directement depuis **Tableau de bord → Plugins → Dépôts**.

## Modèle conseillé (meilleur rapport qualité/coût)

- **`mistral-small-latest`** (recommandé, et configuré par défaut) : excellent rapport qualité/prix pour de la traduction de sous-titres en volume.
- `mistral-medium-latest` : si tu veux un cran de qualité au-dessus pour un coût encore raisonnable.
- `mistral-large-latest` : qualité maximale, coût plus élevé — à réserver aux contenus exigeants.

## Prompt utilisé

Le plugin envoie ce prompt système au modèle (les lignes à traduire sont numérotées dans le message utilisateur, et le modèle doit répondre uniquement en JSON indexé `{"1":"...","2":"...",...}` (clés = numéros des lignes d'entrée, température 0)) :

> You are an expert audiovisual subtitle translator. Translate each numbered line into {langue}. Rules: translate every numbered line; preserve the meaning, tone and register of the dialogue; keep proper nouns, brands and onomatopoeia unchanged unless they are commonly translated; never merge, split, add or remove lines; never add explanations, notes or commentary; write natural, idiomatic {langue} suited to subtitles (concise, readable). Reply ONLY with a JSON object mapping each input number to its translation, e.g. {"1":"...","2":"..."}, containing exactly the keys "1" to "N".

## Configuration

Dans le dashboard Jellyfin (Avancé → Plugins → AI Subtitles) :

- **Clé API Mistral** : votre clé d'API Mistral (obtenue sur [console.mistral.ai](https://console.mistral.ai)).
- **Modèle Mistral** : voir recommandation ci-dessus.
- **Langues cibles** : cochez les langues pour lesquelles générer une traduction.
- **Scan automatique** : activation et intervalle (en heures), limité aux derniers médias ajoutés.
- Bouton **« Lancer un scan des derniers médias maintenant »** pour un scan manuel immédiat.

## Recherche et scan d'un média précis (page du plugin)

Sur la page de configuration du plugin, le champ **« Rechercher un film ou un épisode »** interroge l'API Items de Jellyfin (films + épisodes, 20 résultats) ; chaque résultat a un bouton **« Scanner ce média »** qui appelle `POST /AISubtitles/Scan/Item/{id}`.

## Page « Historique » (Extensions)

Une page dédiée **« AI Subtitles - Historique »** (menu de gauche, section Extensions) liste **tous les fichiers `*-AI.srt`** trouvés dans les dossiers des médiathèques (chemin, taille, date de modification), **50 par page** avec pagination. Les données viennent de `GET /AISubtitles/History?page=N&pageSize=50`.

## Déclenchement manuel (API, admin requis)

- `POST /AISubtitles/Scan/Recent` : scan des derniers médias ajoutés (s'arrête après N médias traités, N configurable, défaut 5).
- `POST /AISubtitles/Scan/Item/{itemId}` : traduction IA pour un film ou un épisode précis.
- `POST /AISubtitles/Scan/Series/{seriesId}` : traduction IA pour une série entière (plafonnée à N épisodes configurables).

## Build

```bash
dotnet build Jellyfin.Plugin.AISubtitles.slnx
```

Le manifeste `build.yaml` permet de packager le plugin pour le dépôt de plugins Jellyfin.
