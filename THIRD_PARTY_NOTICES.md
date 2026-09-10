# Third-party notices

## yt-dlp

ClipPull utilise yt-dlp comme moteur externe téléchargé au moment de l'utilisation depuis sa release GitHub officielle.

Projet : https://github.com/yt-dlp/yt-dlp

Le dépôt source de yt-dlp est sous Unlicense. Les exécutables officiels PyInstaller contiennent aussi des composants tiers et le binaire combiné est soumis à GPLv3+; voir la documentation et `THIRD_PARTY_LICENSES.txt` du projet yt-dlp pour les détails.

ClipPull ne redistribue pas `yt-dlp.exe` dans son dépôt ni dans son binaire.

## FFmpeg / BtbN FFmpeg Builds

Certaines fonctions de ClipPull (audio seul et fusion de pistes pour certaines qualités vidéo) utilisent FFmpeg et ffprobe.

Projet FFmpeg : https://ffmpeg.org/

Build utilisé : https://github.com/BtbN/FFmpeg-Builds

ClipPull v0.3 épingle le build Windows x64 LGPL suivant :

- version : `n9.0.1-26-g5c8e7e2433`
- archive : `ffmpeg-n9.0.1-26-g5c8e7e2433-win64-lgpl-9.0.zip`
- release BtbN : `autobuild-2026-09-06-13-06`
- SHA-256 : `4700c0bcb523466fdf5e36e22ad4ff3fadf33f203e2dbfdc78f5b4cd068b8818`

L'archive n'est pas incluse dans `ClipPull.exe` ni redistribuée par ce dépôt. ClipPull la télécharge à la demande depuis la release BtbN indiquée, vérifie son SHA-256, puis extrait uniquement `ffmpeg.exe` et `ffprobe.exe` dans le profil local de l'utilisateur.

FFmpeg est disponible sous LGPL/GPL selon sa configuration de compilation. Le build sélectionné ici est la variante LGPL publiée par BtbN. Les utilisateurs et redistributeurs doivent consulter les licences et informations de source fournies par FFmpeg/BtbN pour leurs obligations applicables.

## Bootstrap Icons

L'icône source `cloud-arrow-down-fill` provient de Bootstrap Icons.

Projet : https://github.com/twbs/icons

Licence : MIT

Copyright (c) 2019-2024 The Bootstrap Authors.
