# ClipPull

ClipPull est un petit téléchargeur vidéo Windows portable construit autour de [yt-dlp](https://github.com/yt-dlp/yt-dlp).

Objectif : coller un lien, cliquer **Télécharger**, récupérer le fichier localement. Aucun compte ClipPull, aucune télémétrie, aucun serveur ClipPull.

## Télécharger

La version Windows portable est publiée dans **Releases** :

https://github.com/Sd-tech-Sol/ClipPull/releases

Télécharge `ClipPull.exe`, place-le où tu veux et ouvre-le. Il n'y a rien à installer.

## V1

- Windows 10/11 x64
- Un seul `ClipPull.exe` à télécharger
- Aucun installateur
- Interface native WinForms
- Téléchargement dans `Téléchargements\ClipPull` par défaut
- Détection automatique d'une URL copiée dans le presse-papiers au démarrage
- Support des URL acceptées par yt-dlp, avec priorité aux vidéos et Reels Facebook publics
- Option facultative pour utiliser la session d'un navigateur local avec `--cookies-from-browser`
- Moteur yt-dlp téléchargé depuis la release officielle et vérifié par SHA-256 avant utilisation
- Aucune exportation de cookies et aucun envoi vers ClipPull

## Fonctionnement

ClipPull ne réimplémente pas les extracteurs de sites. L'application fournit une interface simple et lance le binaire officiel `yt-dlp.exe` localement.

Au premier téléchargement, ClipPull récupère `yt-dlp.exe` depuis la release stable officielle de yt-dlp et vérifie son hash avec le fichier `SHA2-256SUMS` publié dans la même release. Le moteur est conservé dans `%LOCALAPPDATA%\ClipPull\bin`.

La V1 choisit en priorité un format vidéo MP4 déjà multiplexé afin de rester simple et d'éviter d'imposer FFmpeg. Selon le site, la qualité maximale absolue peut exiger FFmpeg; ce sera ajouté seulement si nécessaire.

## Construire

Prérequis : Windows et .NET 10 SDK.

```powershell
./scripts/generate-icon.ps1
dotnet publish src/ClipPull/ClipPull.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist
```

Le résultat principal est `dist\ClipPull.exe`. GitHub Actions exécute le même principe automatiquement et publie aussi le SHA-256 du binaire.

## Sécurité

- Les arguments sont passés avec `ProcessStartInfo.ArgumentList` plutôt qu'avec une commande concaténée.
- Le binaire yt-dlp est téléchargé uniquement depuis `github.com/yt-dlp/yt-dlp`.
- Le SHA-256 du binaire est comparé au checksum officiel avant remplacement du moteur local.
- ClipPull n'enregistre pas les cookies du navigateur.
- L'option navigateur est désactivée par défaut.

## Limites

Les sites changent régulièrement leurs protections et leurs formats. Une URL peut donc cesser temporairement de fonctionner jusqu'à une mise à jour de yt-dlp. Les contenus privés ou nécessitant une authentification peuvent aussi échouer selon le site ou le navigateur.

Télécharge seulement du contenu que tu as le droit de conserver et respecte les droits d'auteur, les conditions du service et les lois applicables.

## Licence

Le code propre à ClipPull est distribué sous licence MIT. yt-dlp est un projet tiers indépendant avec ses propres licences; ClipPull télécharge son exécutable officiel au moment de l'utilisation plutôt que de le redistribuer dans ce dépôt.

Voir aussi [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
