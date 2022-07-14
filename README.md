# [GlosSI](https://alia5.github.io/GlosSI/) Integration Extension for [Playnite](https://playnite.link/)
This extension automates creating, removing, launching and closing of GlosSI Steam shortcuts for your games in Playnite.

## Why would I want to use this extension?
Apart from all the features that GlosSI offers on its own, this extension makes it easy to use the Steam overlay and Steam input for any game in your Playnite library. Each game can be assigned a separate Steam overlay, allowing for unique controller configurations and making it easier for your Steam friends to see what game you are currently playing. The Steam Overlay can be launched/closed automatically when you launch/close your games. Additionally, when in fullscreen mode a Steam Overlay can be assigned to Playnite itself, making it possible to take advantage of Steam input while navigating your Playnite library.

## Getting started:

### Prerequisites:
Playnite 9, GlosSI and Steam must be installed.

### Configuring settings:
You can modify the behavior of the extension via the settings panel. Only the path settings are required to be set.

#### General settings:
The default configuration to be used when automatically creating Steam shortcuts for games can be changed via the "Edit default GlosSI target configuration" button. This will open the `.json` file used to store configurations. It can either be modified directly or you can replace its contents with a configuration made through the GlosSIConfig user interface.

#### Desktop Mode Settings:
Toggle whether Steam shortcuts should be launched when starting games by default when the application is started.

#### Fullscreen Mode Settings:
One can toggle whether the extension should be used in fullscreen mode.
Additionally, a Playnite Steam Overlay can be used. If configured, this Steam overlay will be used while navigating your Playnite library. To use one, please configure the Steam Shortcut via GlosSIConfig.exe and then enter the name of the shortcut in the settings menu.

#### Path settings (required)
The paths to your Steam `shortcuts.vdf` file[^1] and GlosSI folder[^2] are required.

[^1]: The file that stores information about the non-Steam shortcuts. If it was not automatically found, it can be found in your Steam installation folder, `Steam\userdata\<your ID>\config`
[^2]: The folder where GlosSIConfig.exe and GlosSITarget.exe can be found.

### Using the extension with controllers:
If you have a controller connected, you may want to use Steam's big-picture overlay. To do so, make sure to enable in the Steam "Settings" → "In-Game" → "Use the Big Picture Overlay when using a Steam Input Enabled controller from the desktop." Also check out "Settings" → "Controller" → "General Controller Settings" and enable everything that is relevant to you.


**Tip:** If the name of the Playnite game is identical to the name of a Steam game, community controller mappings for the game can be found in the overlay!

## Usage:
### Adding integration:
For Steam overlays to be launched for games, they have to be added via the right-click menu. Select one or more games, right-click, go to "GlosSI Integration" and click add. The games should now be added (unless they are Steam games, since most Steam games natively support the Steam overlay). If Steam is already running, it must be restarted for the changes to take effect. Games are added irrespective of their installation status.

### Removing integration:
To remove the Steam overlay for games, click on "Remove Integration" instead of "Add Integration".

### Tags:
The extension currently uses two tags, "[GI] Integrated" and "[GI] Ignored". There is generally no reason to touch the "[GI] Integrated", unless you are adding/removing Steam shortcuts manually. The "[GI] Ignored" can be freely removed and added to games. A game with the "[GI] Ignored" will be treated as non-existent to the extension (i.e. no adding/removing of integration and no launching of Steam shortcuts).

### Top Panel Button:
Toggle whether Steam shortcuts should be launched when starting games. The button can also be toggled while in-game.

### Starting/Closing games:
Depending on your settings, when launching a game that has been added (i.e. has the "[GI] Integrated" tag) any currently running GlosSI Steam shortcuts will be closed and the overlay specific to the game will be opened. When closing the game, the game specific overlay will be closed, and if Playnite is running in fullscreen mode with a [Playnite Steam Overlay configured](####Fullscreen-Mode-Settings), that overlay will be launched.

### Limitations:
- If the name of a game in Playnite that has been integrated is changed, the overlay will no longer be able to launch. Simply removing and re-adding the integration should fix this issue.
- If the icon of a game in Playnite that has been integrated is changed, the overlay might no longer be able to find the icon. There are two ways to solve this: remove and re-add the integration, or update the path to the icon in Steam.
- Steam shortcuts will be added to Steam, as such they will be present in your Steam library. Removing them will break the integration, as such it is preferable to simply hide the games in Steam via right-click → Manage → Hide.
- Game names containing UTF-16 characters are currently not supported. This will hopefully be fixed soon.
- Game names that are identical (when illegal file name characters have been removed) will overwrite and use the same `.json` configuration file. This should generally not be a problem.

## Planned features:
- Localization
- Close the game when the shortcut is closed via the overlay.
- Add a default Steam shortcut to use with games that have not been added.
- Automatic creation of GlosSI Target `.json` file for the Playnite overlay (and the planned default Steam shortcut).
- UTF-16 character game name support.

## Acknowledgements:
This extension would not have been possible without JosefNemec and Alia5's amazing work on Playnite and GlosSI respectively! Code from Thomas Pircher and darklinkpower's various extensions was also extremely useful!
