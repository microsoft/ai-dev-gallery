# Publishing a New Version of the AI Dev Gallery App

This document describes the steps to publish a new version of the AI Dev Gallery app to the Microsoft Store. You need to be a member of the AI Dev Gallery App in order to publish a new version of the app.

> Note: The current version of the app is stored in the `version.json` file in the root of the repository. The version number is in the format `X.Y.Z`, where `X` is the major version, `Y` is the minor version, and `Z` is the patch version. If you want to publish a new version of the app with a different version number (for example a new major or minor version), you need to update the version number in the `version.json` file before following the steps below.

1. Make sure you don't have any local uncommited changes, and make sure your local clone is in sync with the remote repository by running the following commands:

    ```console
    git checkout main
    git pull
    ```

2. Ensure that you have the latest version of nbgv installed on your machine. If not, you can install it by running the following command:

    ```console
    dotnet tool install --global nbgv
    ```

3. Run the following command to prepare the release and update the version number in the version.json file:

    ```console
    nbgv prepare-release
    ```

    This will update the version number in the `version.json` file on the current branch (main), and create a commit with an updated version number. By default it will increment the patch version number, but you can find more information about nbgv's cli [here](https://github.com/dotnet/Nerdbank.GitVersioning/blob/main/doc/nbgv-cli.md).

    It will also create a new branch with the name `rel/vX.Y.Z` where `X.Y.Z` is the `current` version number. This is the release branch, which will be automatically checked out after the command is executed.

4. Now you can push the changes to the remote repository by running the following command:

    ```console
    git push -u origin rel/vX.Y.Z
    ```

5. To make sure eveybody is now working on the new release, checkout the main branch and push the changes to the remote repository:

    ```console
    git checkout main
    git push
    ```
    > Note: This will automatically spin up a new release build in ADO, which automatically uploads the new version to the Microsoft Store.