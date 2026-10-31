# BetterOtherRole: MiraAPI

Base solide pour un mod Among Us construit sur [MiraAPI](https://github.com/All-Of-Us-Gaming/MiraAPI) (BepInEx IL2CPP + Reactor).

## Pile technique

- .NET 6 / C# (`net6.0`)
- BepInEx.Unity.IL2CPP `6.0.0-be.735`
- Reactor `2.5.0-ci.371`
- MiraAPI (`AllOfUs.MiraAPI`) `0.3.9-ci.904`
- Assemblies du jeu via `AmongUs.GameLibs.Steam` (NuGet, pas besoin d'installation locale pour compiler)

Ces versions sont alignées avec celles utilisées par TOU-Mira / Bor-Mira dans ce même workspace : elles sont connues pour fonctionner ensemble.

## Structure

```
BetterOtherRoleMiraAPI.csproj   Projet principal
Plugin.cs                       Point d'entrée du plugin (BepInAutoPlugin + IMiraPlugin)
GlobalUsings.cs                 Usings globaux (MiraAPI, logger Reactor)
modinfo.json                    Métadonnées du mod (nom, auteurs, liens) lues par MiraAPI
AmongUs.props                   Références NuGet partagées + copie auto du DLL après build
Directory.Build.props           Propriétés MSBuild communes (TFM, version, auteurs...)
nuget.config                    Flux NuGet (BepInEx, Reactor, Samboy063, nuget.org)
```

## Compiler

```
dotnet build
```

Le premier restore télécharge MiraAPI/Reactor/BepInEx/les assemblies du jeu ; les builds suivants sont rapides.

## Copier automatiquement le DLL dans le jeu

Définis la variable d'environnement (ou propriété MSBuild) `AmongUs` sur le dossier d'installation Among Us, puis rebuild :

```
$env:AmongUs = "C:\Program Files (x86)\Steam\steamapps\common\Among Us"
dotnet build
```

Le DLL sera copié dans `BepInEx/plugins/` après chaque build.

## Prochaines étapes

- Ajouter des rôles via l'API de rôles de MiraAPI (ex. `CustomRole`, `RoleBehaviour`).
- Ajouter des patches Harmony dans un dossier `Patches/`.
- Ajouter des options via `MiraAPI.GameOptions`.
