# Development Instructions

To ensure the project correctly references the Dyson Sphere Program files, developers need to follow these steps:

1. Locate the `DefaultPath.props.example` file in the root directory of the project.
2. Copy and rename the `DefaultPath.props.example` file to `DefaultPath.props`.
3. Open the `DefaultPath.props` file and manually modify the value of the `DysonSphereProgramPath` property to your local Dyson Sphere Program installation path. For example:

    ```xml
    <PropertyGroup>
      <DysonSphereProgramPath>D:\SteamLibrary\steamapps\common\Dyson Sphere Program\DSPGAME_Data\Managed</DysonSphereProgramPath>
    </PropertyGroup>
    ```

After completing the above steps, the project will be able to correctly reference the Dyson Sphere Program files.
