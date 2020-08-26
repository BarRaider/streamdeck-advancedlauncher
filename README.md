# Advanced Launcher
 Smart application launcher for the Elgato Stream Deck

**Author's website and contact information:** [https://barraider.com](https://barraider.com)

### New in v1.4
- **STEAM SUPPORT**: New `Steam Game Launcher` action shows you a list of all Steam games installed and let's you launch them from the Stream Deck
- Improved initial image rendering times
- "Windows Shield" icon is added to image on key when `Run as Administrator` is enabled

### New in v1.3
- New `Bring To Front` feature allows you to bring an app to front if it's already running (no longer need to use Windows Mover & Resizer). To enable, make sure `Max Instances` is enabled.
- Shows an :green_circle:  indicator if the app is already running
- Improved icon scaling when Advanced Launcher fetched the icon from the application being launched.

## Current Features
- New `Start In` parameter, allows you to modify the working directory in which the application runs. 
- Programs can now be set to `Run as Administrator`
- `Limit number of instances running` feature. Set limit to 1 to ensure you don't launch the app if it's already running.
- `Kill all existing instances` ensures only the freshly launched instance is running.
- Customize the arguments to pass to launched application
- Process Killer action allows killing all instances of an application

### Download

* [Download plugin](https://github.com/BarRaider/streamdeck-advancedlauncher/releases/)

## I found a bug, who do I contact?
For support please contact the developer. Contact information is available at https://barraider.com

## I have a feature request, who do I contact?
Please contact the developer. Contact information is available at https://barraider.com

## Dependencies
* Uses StreamDeck-Tools by BarRaider: [![NuGet](https://img.shields.io/nuget/v/streamdeck-tools.svg?style=flat)](https://www.nuget.org/packages/streamdeck-tools)
* Uses [Easy-PI](https://github.com/BarRaider/streamdeck-easypi) by BarRaider - Provides seamless integration with the Stream Deck PI (Property Inspector) 



