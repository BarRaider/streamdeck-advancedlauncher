# Advanced Launcher
 Smart application launcher for the Elgato Stream Deck

**Author's website and contact information:** [https://barraider.com](https://barraider.com)

### New in v1.8
- Added the ability to launch applications in the background

## Current Features
- New `Start In` parameter, allows you to modify the working directory in which the application runs. 
- Programs can now be set to `Run as Administrator`
- `Limit number of instances running` feature. Set limit to 1 to ensure you don't launch the app if it's already running.
- `Kill all existing instances` ensures only the freshly launched instance is running.
- Customize the arguments to pass to launched application
- `Bring To Front` feature allows you to bring an app to front if it's already running (To enable, make sure `Max Instances` is enabled).
- `Background` feature allows you to launch an application in the background (Some applications may not support this feature).
- Shows a :green_circle: indicator if the app is already running
- Process Killer action allows killing all instances of an application
- **STEAM SUPPORT**: New `Steam Game Launcher` action shows you a list of all Steam games installed and let's you launch them from the Stream Deck
- Support for **Microsoft Store Apps (UWP)**

### Download

* [Download plugin](https://github.com/BarRaider/streamdeck-advancedlauncher/releases/)

## I found a bug, who do I contact?
For support please contact the developer. Contact information is available at https://barraider.com

## I have a feature request, who do I contact?
Please contact the developer. Contact information is available at https://barraider.com

## Dependencies
* Uses StreamDeck-Tools by BarRaider: [![NuGet](https://img.shields.io/nuget/v/streamdeck-tools.svg?style=flat)](https://www.nuget.org/packages/streamdeck-tools)
* Uses [Easy-PI](https://github.com/BarRaider/streamdeck-easypi) by BarRaider - Provides seamless integration with the Stream Deck PI (Property Inspector) 



