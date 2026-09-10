# ClipPull

ClipPull est un téléchargeur vidéo Windows portable construit autour de [yt-dlp](https://github.com/yt-dlp/yt-dlp).

Objectif : coller un ou plusieurs liens, cliquer **Télécharger tout**, récupérer les fichiers localement. Aucun compte ClipPull, aucune télémétrie, aucun serveur ClipPull.

## Télécharger

La version Windows portable est publiée dans **Releases** :

https://github.com/Sd-tech-Sol/ClipPull/releases

Télécharge `ClipPull.exe`, place-le où tu veux et ouvre-le. Il n'y a rien à installer.

## Plateformes

ClipPull transmet les URL à yt-dlp. Il peut donc fonctionner avec les sites pris en charge par yt-dlp, notamment Facebook/Reels, TikTok, Instagram/Reels, YouTube/Shorts, X/Twitter, Vimeo et plusieurs autres sites.

Le support réel dépend de yt-dlp et peut changer lorsqu'une plateforme modifie son site ou ses protections.

## Fonctionnalités v0.2.0

- Windows 10/11 x64
- Un seul `ClipPull.exe` portable à télécharger
- Aucun installateur
- Interface native WinForms
- Plusieurs liens à la fois, un par ligne
- Téléchargement séquentiel : les liens sont traités un après l'autre
- Détection et suppression automatique des doublons exacts
- File visible avec plateforme et statut de chaque lien
- Progression du téléchargement en cours et progression `x/y` de la file
- Un échec n'arrête pas les liens suivants
- Bouton **Réessayer les échecs**
- Import d'une liste de liens depuis un fichier `.txt`
- Détection automatique des liens copiés dans le presse-papiers au démarrage
- Téléchargement dans `Téléchargements\ClipPull` par défaut
- Option facultative pour utiliser la session d'un navigateur local avec `--cookies-from-browser`
- Chrome, Edge, Firefox, Brave, Chromium, Opera et Vivaldi proposés dans l'interface
- Moteur yt-dlp téléchargé depuis la release officielle et vérifié par SHA-256 avant utilisation
- Aucune exportation de cookies et aucun envoi vers ClipPull

## Fonctionnement

ClipPull ne réimplémente pas les extracteurs de sites. L'application fournit une interface simple et lance le binaire officiel `yt-dlp.exe` localement.

Au premier téléchargement, ClipPull récupère `yt-dlp.exe` depuis la release stable officielle de yt-dlp et vérifie son hash avec le fichier `SHA2-256SUMS` publié dans la même release. Le moteur est conservé dans `%LOCALAPPDATA%\ClipPull\bin`.

La version actuelle choisit en priorité un format vidéo MP4 déjà multiplexé afin de rester simple et d'éviter d'imposer FFmpeg. Selon le site, la qualité maximale absolue ou certaines fonctions audio peuvent exiger FFmpeg.

## Idées pour les prochaines versions

Les fonctions suivantes sont volontairement laissées hors de la v0.2.0 jusqu'à ce qu'elles apportent une vraie valeur sans alourdir l'application :

- mode **Audio seulement** avec gestion propre de FFmpeg
- choix de qualité `Auto / 1080p / 720p / plus petit fichier`
- option explicite pour télécharger une playlist complète
- glisser-déposer d'un fichier `.txt`
- historique local facultatif pour éviter de retélécharger deux fois le même contenu
- bouton **Copier le rapport d'erreur** pour faciliter le dépannage

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
- Les liens sont traités localement et ne sont pas envoyés à un serveur ClipPull.

## Limites

Les sites changent régulièrement leurs protections et leurs formats. Une URL peut donc cesser temporairement de fonctionner jusqu'à une mise à jour de yt-dlp. Les contenus privés ou nécessitant une authentification peuvent aussi échouer selon le site ou le navigateur.

Télécharge seulement du contenu que tu as le droit de conserver et respecte les droits d'auteur, les conditions du service et les lois applicables.

## Licence

Le code propre à ClipPull est distribué sous licence MIT. yt-dlp est un projet tiers indépendant avec ses propres licences; ClipPull télécharge son exécutable officiel au moment de l'utilisation plutôt que de le redistribuer dans ce dépôt.

Voir aussi [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
