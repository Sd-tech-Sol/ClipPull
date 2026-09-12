# ClipPull

*Read this in [English](README.md).*

ClipPull est un téléchargeur média Windows portable construit autour de [yt-dlp](https://github.com/yt-dlp/yt-dlp). Colle un ou plusieurs liens, choisis quelques options simples, clique **Télécharger tout**, puis récupère les fichiers localement. Aucun compte ClipPull, aucune télémétrie et aucun serveur ClipPull ne sont requis.

## Télécharger

Les versions Windows portables sont publiées dans [GitHub Releases](https://github.com/Sd-tech-Sol/ClipPull/releases). Télécharge `ClipPull.exe`, place-le où tu veux et ouvre-le. Aucun installateur n'est requis.

## Plateformes

ClipPull transmet les URL à yt-dlp et peut donc fonctionner avec les sites pris en charge par ce moteur, notamment Facebook/Reels, TikTok, Instagram/Reels, YouTube/Shorts, X/Twitter, Vimeo et plusieurs autres. Le support réel peut changer lorsqu'une plateforme modifie son site ou ses protections.

## Fonctions principales

- Windows 10/11 x64 et un seul `ClipPull.exe` portable
- Interface en anglais et en français; l’anglais est utilisé au premier lancement et la langue se change instantanément dans les paramètres
- Plusieurs liens à la fois, traités séquentiellement
- File visible avec vitesse/temps restant, retrait des éléments en attente ou terminés, suppression des doublons et réessai des échecs
- Choix de qualité vidéo : Auto, meilleure, 1080p, 720p, 480p ou petit fichier; Auto privilégie un flux prêt à l’emploi et utilise une fusion FFmpeg vérifiée seulement au besoin
- Audio seulement en M4A ou MP3
- Playlists facultatives avec limite de sécurité configurable
- Import ou glisser-déposer de listes de liens `.txt`
- Historique local facultatif avec `--download-archive` de yt-dlp
- Rapport d'erreurs copiable
- Aperçu avec titre, plateforme, durée et miniature lorsque disponibles
- Utilisation facultative d'une session de navigateur avec `--cookies-from-browser`
- Préférences et taille/état de la fenêtre conservés dans `%LOCALAPPDATA%\ClipPull\settings.json`
- Avis non bloquants de mise à jour de ClipPull provenant des releases GitHub officielles, sans téléchargement ni remplacement automatique de l’exécutable
- Section À propos avec version, licence, remerciements et liens du projet

## Mises à jour automatiques des dépendances

Au démarrage, ClipPull vérifie ses composants sans bloquer l'interface.

- **yt-dlp** est comparé à sa release GitHub officielle. Une nouvelle version n'est installée qu'après validation de son SHA-256 publié.
- **FFmpeg**, lorsqu'il est déjà installé, est comparé à la version approuvée dans [`dependencies.json`](dependencies.json). Toute nouvelle archive doit provenir de `BtbN/FFmpeg-Builds` et réussir la vérification SHA-256 avant activation.
- FFmpeg n'est pas téléchargé au démarrage pour quelqu'un qui n'en a jamais eu besoin. Il est installé à la demande pour les modes audio ou vidéo qui l'exigent.
- Une panne réseau ne bloque pas ClipPull lorsqu'une dépendance locale valide est déjà disponible.

## Sécurité et confidentialité

ClipPull ne réimplémente pas les extracteurs des plateformes. Il fournit une interface locale et lance le binaire officiel `yt-dlp.exe`.

- Les URL et paramètres sont transmis avec `ProcessStartInfo.ArgumentList` plutôt que par concaténation d'une commande shell.
- Les téléchargements de yt-dlp et FFmpeg sont vérifiés par hash avant installation ou remplacement.
- yt-dlp est lancé avec `--ignore-config` et `--no-plugin-dirs` afin de ne pas charger implicitement des configurations ou plugins externes.
- Les cookies du navigateur sont facultatifs et désactivés par défaut; ClipPull ne les exporte pas lui-même.
- L'historique et les réglages propres à ClipPull restent locaux.

## Construire

Prérequis : Windows et .NET 10 SDK.

```powershell
./scripts/generate-icon.ps1
dotnet publish src/ClipPull/ClipPull.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist
```

GitHub Actions construit aussi automatiquement l'exécutable Windows, calcule son SHA-256 et publie ou rafraîchit la release correspondant à la version du projet.

## Développement assisté par IA

ClipPull est un projet original de **Sébastien Dubé**. L'idée, la direction du produit, les exigences, les priorités, les approbations et les décisions finales lui appartiennent.

Le développement a été assisté par **OpenAI ChatGPT**, **Anthropic Claude Code** et **OpenAI Codex**, selon les rôles décrits dans la divulgation complète. Aucun outil d'IA n'est auteur, propriétaire ou mainteneur, et leur utilisation n'implique aucune affiliation avec OpenAI ou Anthropic ni aucune approbation de leur part.

**La responsabilité et la maintenance finales reviennent à Sébastien Dubé.** Voir [`AI_ASSISTANCE.md`](AI_ASSISTANCE.md) pour la divulgation complète.

## Limites

Les plateformes changent régulièrement leurs protections et leurs formats. Une URL peut cesser temporairement de fonctionner jusqu'à une mise à jour de yt-dlp. Le contenu authentifié peut aussi dépendre de la plateforme et du navigateur. Télécharge seulement du contenu que tu as le droit de conserver et respecte les droits d'auteur, les conditions du service et les lois applicables.

## Licence

Le code propre à ClipPull est distribué sous [licence MIT](LICENSE). yt-dlp et FFmpeg sont des projets tiers indépendants avec leurs propres licences; leurs binaires ne sont pas inclus dans `ClipPull.exe`.

Voir [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
