# 开发说明

为了使项目能够正确引用 Dyson Sphere Program 的相关文件，开发者需要进行以下配置：

1. 在项目根目录下找到 `DefaultPath.props.example` 文件。
2. 将 `DefaultPath.props.example` 文件复制并重命名为 `DefaultPath.props`。
3. 打开 `DefaultPath.props` 文件，手动修改 `DysonSphereProgramPath` 属性的值为你本地的 Dyson Sphere Program 安装路径。例如：

    ```xml
    <PropertyGroup>
      <DysonSphereProgramPath>D:\SteamLibrary\steamapps\common\Dyson Sphere Program\DSPGAME_Data\Managed</DysonSphereProgramPath>
    </PropertyGroup>
    ```

完成以上步骤后，项目将能够正确引用 Dyson Sphere Program 的相关文件。
