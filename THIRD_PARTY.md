# Third-party dependencies

| Dependency | Version / source | License |
|---|---|---|
| Unity | 6000.6.4f1, installed Universal 2D template | Unity terms |
| R3 core | 1.3.1, [official NuGet package](https://www.nuget.org/packages/R3/1.3.1) | MIT; `Assets/Plugins/R3/LICENSE-R3.txt` |
| R3.Unity | 1.3.1, [Cysharp/R3](https://github.com/Cysharp/R3/tree/1.3.1/src/R3.Unity/Assets/R3.Unity) through UPM | MIT |
| Microsoft.Bcl.TimeProvider | 8.0.0, official NuGet, netstandard2.0 | MIT; `Assets/Plugins/R3/LICENSE-dotnet.txt` |
| System.Threading.Channels | 8.0.0, official NuGet, netstandard2.1 | MIT |
| System.ComponentModel.Annotations | 5.0.0, official NuGet, netstandard2.1 | MIT |
| System.Runtime.CompilerServices.Unsafe | 6.0.0, official NuGet, netstandard2.0 | MIT |
| DOTween Free | 1.3.030, [Demigiant official download](https://dotween.demigiant.com/download.php) | [DOTween license](https://dotween.demigiant.com/license.php); distributed package readme retained |
| Noto Sans CJK JP Regular | [notofonts/noto-cjk](https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/Japanese), OTF | SIL Open Font License 1.1; `Assets/Fonts/OFL.txt` |

R3 core binaries were extracted from the official NuGet archives, with the .NET Standard 2.1 build selected where available. The Unity adapters use the pinned official Git UPM package. Unity 6 provides the AsyncInterfaces compatibility assembly. DOTween was imported and configured through the Editor.

The runtime uses DOTween core APIs directly; it does not depend on the modules' UI extension assembly. NuGet DLLs are checked in so the sample does not require an additional NuGet package manager.

Noto's generated SDF asset is under `Assets/Fonts/`. The source font and license are retained so the atlas can be regenerated. No external art or sound recordings are used.
