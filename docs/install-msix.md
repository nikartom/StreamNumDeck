# Installing the StreamNumDeck MSIX / Установка StreamNumDeck MSIX

## English

The current GitHub beta is self-contained and targets Windows 11 x64. It is signed with a development certificate, so Windows requires that certificate to be trusted before the package can be installed.

1. Download the `.msix`, `.cer`, and `SHA256SUMS.txt` files from the same GitHub release.
2. Verify that the MSIX checksum matches `SHA256SUMS.txt`:

   ```powershell
   Get-FileHash .\StreamNumDeck_0.1.0.0_x64.msix -Algorithm SHA256
   ```

3. Open PowerShell **as Administrator** in the download directory and import the public certificate into the local computer's Trusted People store:

   ```powershell
   $certificate = Import-Certificate `
       -FilePath .\StreamNumDeck-Development-Signing.cer `
       -CertStoreLocation Cert:\LocalMachine\TrustedPeople

   $certificate.Thumbprint
   ```

4. For release `v0.1.0`, the expected certificate thumbprint is:

   ```text
   C340046FA11736792CC65D92F0B889A3FE2DCA85
   ```

5. Double-click the `.msix` file and choose **Install**.

Only trust a certificate obtained from the official StreamNumDeck repository. A certificate in the local computer store affects every user of that computer. This manual trust step is required only because the beta is test-signed; a future production-trusted or Microsoft Store package will not require it.

## Русский

Текущая beta-версия на GitHub является автономной и предназначена для Windows 11 x64. Пакет подписан сертификатом разработчика, поэтому перед установкой Windows должна явно доверять этому сертификату.

1. Загрузите файлы `.msix`, `.cer` и `SHA256SUMS.txt` из одного и того же выпуска GitHub.
2. Убедитесь, что контрольная сумма MSIX совпадает со значением в `SHA256SUMS.txt`:

   ```powershell
   Get-FileHash .\StreamNumDeck_0.1.0.0_x64.msix -Algorithm SHA256
   ```

3. Откройте PowerShell **от имени администратора** в папке загрузки и импортируйте публичный сертификат в хранилище доверенных лиц локального компьютера:

   ```powershell
   $certificate = Import-Certificate `
       -FilePath .\StreamNumDeck-Development-Signing.cer `
       -CertStoreLocation Cert:\LocalMachine\TrustedPeople

   $certificate.Thumbprint
   ```

4. Для выпуска `v0.1.0` ожидаемый отпечаток сертификата:

   ```text
   C340046FA11736792CC65D92F0B889A3FE2DCA85
   ```

5. Откройте файл `.msix` двойным кликом и нажмите **Установить**.

Доверяйте только сертификату из официального репозитория StreamNumDeck. Сертификат в хранилище локального компьютера действует для всех его пользователей. Ручной импорт нужен только из-за тестовой подписи beta-версии; пакет с доверенной производственной подписью или из Microsoft Store в этом не нуждается.
