# Cats are Online
A multiplayer mod for [Cats are Liquid - A Better Place](https://store.steampowered.com/app/1188080)

## Installation
### Playing
1. Install [Cats are Liquid API](https://github.com/cgytrus/CalApi)
2. Install [Cats are Online](https://github.com/cgytrus/CatsAreOnline/releases)
   the same way as Cats are Online API (look for the latest one **without** "Server")
3. Open the game and press F1 when you get to the main menu
4. Open the "Cats are Online" category
5. Change the Username and Display Name options to whatever you like (you can use [Rich Text](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/StyledText.html),
   however, keep in mind that username is the name that people use in commands, so if you use rich text in your username,
   it'll be hard if even possible to type, so don't use rich text in usernames please)
6. Change the Address option to the address of the server you're connecting to
   (thanks to [sbeve](https://github.com/svtetering), there's now an official server running at **`cao.cgyt.ru`**)
7. Enable the Connected option
8. Press T to chat, type /help in chat for a list of available commands

### Hosting a server
1. Forward the port you want to use (or enable UPnP in config.json after running the server for the first time, default port is 1337)
2. Download the [server](https://github.com/cgytrus/CatsAreOnline/releases)
3. Run CatsAreOnlineServer.exe through Powershell or cmd if you're on Windows or CatsAreOnlineServer through terminal if you're on Linux

## Contributing
1. Clone the repository
2. Put the missing DLLs into CatsAreOnline/libs (for a more detailed explanation,
   follow the [Plugin development](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/1_setup.html)
   guide on the BepInEx wiki starting from Gathering DLL dependencies)

## TODO
*Entries ending with a question mark is stuff i may or may not do later*
- sync cat features (tail, face etc.)?
- server list?
- permission system?
- show speedrun time of a player if they're speedrunning
- fix a bug where the ice sprite doesn't go away sometimes???? huh??
