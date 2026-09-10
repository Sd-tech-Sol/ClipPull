# ClipPull

*Read this in [English](README.md).*

ClipPull est un petit téléchargeur média Windows portable construit autour de [yt-dlp](https://github.com/yt-dlp/yt-dlp).

L'objectif est simple : coller un ou plusieurs liens, choisir quelques options simples, cliquer **Télécharger tout**, puis récupérer les fichiers localement. Aucun compte ClipPull, aucune télémétrie et aucun serveur ClipPull.

## Télécharger

La version Windows portable est publiée dans **Releases** :

https://github.com/Sd-tech-Sol/ClipPull/releases

Télécharge `ClipPull.exe`, place-le où tu veux et ouvre-le. Aucun installateur n'est requis.

## Plateformes

ClipPull transmet les URL au moteur yt-dlp. Il peut donc fonctionner avec les sites pris en charge par yt-dlp, notamment Facebook/Reels, TikTok, Instagram/Reels, YouTube/Shorts, X/Twitter, Vimeo et plusieurs autres sites.

Le support réel dépend de yt-dlp et peut changer lorsqu'une plateforme modifie son site ou ses protections.

## Nouveauté v0.3.1 : mises à jour automatiques des dépendances

Au démarrage, ClipPull affiche immédiatement son interface puis vérifie automatiquement ses composants :

- **yt-dlp** : ClipPull consulte la release officielle, compare le SHA-256 publié et remplace automatiquement le moteur local seulement si le nouveau binaire correspond au checksum officiel.
- **FFmpeg** : si FFmpeg est déjà installé, ClipPull consulte `dependencies.json`, le manifeste de versions approuvées du dépôt. Si une version approuvée plus récente est disponible, l'archive est téléchargée, son SHA-256 est vérifié, puis la nouvelle version devient active. L'ancienne n'est supprimée qu'après une installation réussie.
- Si FFmpeg n'a jamais été utilisé, il n'est pas téléchargé inutilement au démarrage. Il sera installé automatiquement au premier besoin audio/qualité élevée.
- Une panne réseau ou une erreur de mise à jour ne bloque pas ClipPull : la dernière copie locale valide reste utilisable.
- Une archive dont le SHA-256 ne correspond pas n'est jamais installée.

Le manifeste FFmpeg n'accepte que des URL HTTPS du dépôt officiel `BtbN/FFmpeg-Builds`, des builds Windows x64 LGPL et des SHA-256 valides.

## Les 7 ajouts de la v0.3.0

1. **Choix de qualité vidéo** — Auto (rapide), meilleure qualité, 1080p max, 720p max, 480p max ou petit fichier.
2. **Audio seulement** — extraction en M4A ou MP3. FFmpeg est installé localement seulement si cette fonction est utilisée.
3. **Playlists complètes** — option explicite, désactivée par défaut, avec confirmation et limite configurable par lien (50 éléments par défaut) pour éviter un téléchargement massif accidentel.
4. **Glisser-déposer d'un fichier `.txt`** — dépose directement dans la fenêtre un fichier contenant des liens; l'import classique reste disponible.
5. **Historique local facultatif** — activé par défaut avec le mécanisme `--download-archive` de yt-dlp afin d'éviter de retélécharger le même contenu. L'historique peut être effacé dans l'interface sans supprimer les médias déjà téléchargés.
6. **Rapport d'erreurs copiable** — les échecs sont conservés pour la session et peuvent être copiés en un clic, avec plateforme, URL et message utile. Le bouton de réessai des échecs reste disponible.
7. **Aperçu avant téléchargement** — le premier lien peut être analysé pour afficher son titre, sa plateforme, sa durée lorsqu'elle est disponible et sa miniature.

## Fonctions déjà présentes

- Windows 10/11 x64
- Un seul `ClipPull.exe` portable
- Plusieurs liens à la fois, un par ligne
- Téléchargements traités séquentiellement
- Détection et suppression des doublons exacts dans la liste
- File visible avec plateforme et statut de chaque lien
- Progression du téléchargement en cours et progression `x/y`
- Un échec n'arrête pas les liens suivants
- Bouton **Réessayer les échecs**
- Import de listes depuis `.txt`
- Détection des liens présents dans le presse-papiers au démarrage
- Dossier par défaut `Téléchargements\ClipPull`
- Option facultative pour utiliser une session de navigateur locale avec `--cookies-from-browser`
- Chrome, Edge, Firefox, Brave, Chromium, Opera et Vivaldi proposés
- yt-dlp téléchargé depuis sa release officielle et vérifié par SHA-256

## FFmpeg : à la demande et version approuvée

Les modes audio et les qualités qui peuvent nécessiter la fusion de pistes ont besoin de FFmpeg. ClipPull ne gonfle pas son EXE avec FFmpeg.

Au premier usage d'une fonction qui l'exige, ClipPull demande confirmation puis récupère la version approuvée dans [`dependencies.json`](dependencies.json). Si le manifeste n'est pas joignable lors d'une première installation, ClipPull conserve une version de secours épinglée dans le code. L'archive est vérifiée par SHA-256 avant extraction. Seuls `ffmpeg.exe` et `ffprobe.exe` sont extraits dans `%LOCALAPPDATA%\ClipPull\ffmpeg`.

Le manifeste initial approuve : `n9.0.1-26-g5c8e7e2433`

SHA-256 initial de l'archive : `4700c0bcb523466fdf5e36e22ad4ff3fadf33f203e2dbfdc78f5b4cd068b8818`

Après une mise à jour réussie, `active-version.txt` indique localement la version FFmpeg active. Une installation interrompue ou invalide ne remplace pas la version active précédente.

## Historique local

Lorsque l'option d'historique est activée, ClipPull utilise directement le fichier d'archive de yt-dlp :

`%LOCALAPPDATA%\ClipPull\download-archive.txt`

Ce fichier contient les identifiants nécessaires pour reconnaître du contenu déjà téléchargé. Il peut être supprimé avec **Effacer historique**. Aucun historique n'est envoyé à ClipPull ou à un serveur ClipPull.

## Fonctionnement et sécurité

ClipPull ne réimplémente pas les extracteurs des plateformes. Il fournit une interface locale et lance le binaire officiel `yt-dlp.exe`.

- Les URL et autres paramètres sont passés avec `ProcessStartInfo.ArgumentList`, pas par concaténation d'une ligne de commande.
- yt-dlp est téléchargé uniquement depuis sa release GitHub officielle et son SHA-256 est vérifié avant installation/remplacement.
- ClipPull lance yt-dlp avec `--ignore-config` et `--no-plugin-dirs` pour ne pas charger implicitement des configurations ou plugins yt-dlp externes.
- L'utilisation des cookies du navigateur est facultative et désactivée par défaut; ClipPull ne les exporte pas lui-même.
- FFmpeg est piloté par un manifeste de versions approuvées, limité à `BtbN/FFmpeg-Builds`, et chaque archive est vérifiée par SHA-256 avant extraction.
- Les données propres à ClipPull restent locales. Les requêtes nécessaires au téléchargement, à l'aperçu et aux vérifications de mises à jour sont envoyées aux plateformes concernées, à GitHub et aux sources officielles des moteurs.

## Construire

Prérequis : Windows et .NET 10 SDK.

```powershell
./scripts/generate-icon.ps1
dotnet publish src/ClipPull/ClipPull.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist
```

Le résultat principal est `dist\ClipPull.exe`. GitHub Actions effectue aussi automatiquement le build Windows, calcule le SHA-256 du binaire et publie/rafraîchit la release correspondant à la version du projet afin qu'elle pointe sur le dernier commit de cette version.

## Limites

Les plateformes changent régulièrement leurs protections et leurs formats. Une URL peut cesser temporairement de fonctionner jusqu'à une mise à jour de yt-dlp. Un aperçu ou un contenu nécessitant une authentification peut également échouer selon la plateforme ou le navigateur.

Le mode playlist est volontairement limité par défaut. Augmenter cette limite peut télécharger beaucoup de données.

Télécharge seulement du contenu que tu as le droit de conserver et respecte les droits d'auteur, les conditions du service et les lois applicables.

## Licence

Le code propre à ClipPull est distribué sous licence MIT. yt-dlp et FFmpeg sont des projets tiers indépendants avec leurs propres licences; leurs binaires ne sont pas inclus dans `ClipPull.exe`.

Voir [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
