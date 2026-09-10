# Code signing

*Lire en français plus bas / French version below.*

ClipPull's GitHub Actions workflow is ready to sign `ClipPull.exe` automatically with **Microsoft Azure Artifact Signing** (formerly Trusted Signing). Signing is performed after the single-file build and before the final SHA-256 and GitHub Release are generated.

Once Artifact Signing is configured, every non-PR release build uses GitHub OpenID Connect (OIDC), so no long-lived Azure password or client secret is stored in GitHub.

## One-time setup

1. Use a paid Azure subscription and create an **Artifact Signing** account.
2. Complete Microsoft's **Public Trust individual identity validation**.
3. Create a **Public Trust certificate profile**.
4. Create or use a Microsoft Entra application/service principal, add a GitHub OIDC federated credential for repository `Sd-tech-Sol/ClipPull`, and grant it the **Artifact Signing Certificate Profile Signer** role for the signing resource/profile.
5. In GitHub repository **Settings → Secrets and variables → Actions → Variables**, create these six repository variables:

   - `AZURE_CLIENT_ID`
   - `AZURE_TENANT_ID`
   - `AZURE_SUBSCRIPTION_ID`
   - `ARTIFACT_SIGNING_ENDPOINT`
   - `ARTIFACT_SIGNING_ACCOUNT_NAME`
   - `ARTIFACT_SIGNING_CERTIFICATE_PROFILE_NAME`

No Azure client secret is required by the ClipPull workflow.

If none of the six variables is configured, builds remain unsigned. If only some are configured, the workflow stops instead of silently publishing an unsigned release. When all six are configured, the workflow signs `ClipPull.exe`, verifies the Authenticode signature, calculates the final SHA-256 on the signed file, and then publishes or refreshes the GitHub Release.

Microsoft SmartScreen reputation can still take time to build for a new publisher even with a valid signature. Keeping the same verified publisher identity across releases lets that reputation accumulate over time.

---

# Signature du code

Le workflow GitHub Actions de ClipPull est prêt à signer automatiquement `ClipPull.exe` avec **Microsoft Azure Artifact Signing** (anciennement Trusted Signing). La signature est effectuée après la compilation single-file et avant le calcul du SHA-256 final et la publication de la GitHub Release.

Une fois Artifact Signing configuré, chaque build de release utilise **OpenID Connect (OIDC)** de GitHub. Aucun mot de passe Azure ni secret client longue durée n'est donc conservé dans GitHub.

## Configuration à faire une seule fois

1. Utiliser un abonnement Azure payant et créer un compte **Artifact Signing**.
2. Compléter la **validation d'identité individuelle Public Trust** de Microsoft.
3. Créer un **profil de certificat Public Trust**.
4. Créer ou utiliser une application/service principal Microsoft Entra, ajouter une identité fédérée GitHub OIDC pour le dépôt `Sd-tech-Sol/ClipPull`, puis lui attribuer le rôle **Artifact Signing Certificate Profile Signer** sur la ressource/le profil de signature.
5. Dans GitHub, ouvrir **Settings → Secrets and variables → Actions → Variables** et créer ces six variables de dépôt :

   - `AZURE_CLIENT_ID`
   - `AZURE_TENANT_ID`
   - `AZURE_SUBSCRIPTION_ID`
   - `ARTIFACT_SIGNING_ENDPOINT`
   - `ARTIFACT_SIGNING_ACCOUNT_NAME`
   - `ARTIFACT_SIGNING_CERTIFICATE_PROFILE_NAME`

Le workflow ClipPull n'a besoin d'aucun secret client Azure.

Si aucune des six variables n'est configurée, les builds restent non signés. Si seulement une partie est configurée, le workflow s'arrête plutôt que de publier silencieusement un EXE non signé. Lorsque les six sont présentes, GitHub signe `ClipPull.exe`, vérifie la signature Authenticode, calcule le SHA-256 final du fichier signé, puis publie ou rafraîchit automatiquement la release.

Même avec une signature valide, la réputation Microsoft SmartScreen d'un nouvel éditeur peut prendre un certain temps à se bâtir. Garder la même identité d'éditeur vérifiée d'une version à l'autre permet à cette réputation de s'accumuler.
