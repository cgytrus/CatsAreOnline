# Cats are Online
A multiplayer mod for the game [Cats are Liquid: A Better Place](https://store.steampowered.com/app/1188080)

## Installation
### Playing
1. Install [BepInEx](https://bepinex.github.io/bepinex_docs/master/articles/user_guide/installation) (x64)
2. Install [Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager/releases/latest)
by drag-and-dropping the folder from the downloaded archive into the game's folder
3. Install [Cats are Online](https://github.com/cgytrus/CatsAreOnline/releases) the same way as Configuration Manager
4. Open the game and press F1 when you get to the main menu
5. Open the "Cats are Online" category
6. Change the Username and Display Name options to whatever you like (you can use [Rich Text](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/StyledText.html),
   however, keep in mind that username is the name that people use in commands, so if you use rich text in your username,
   it'll be hard if even possible to type, so don't use rich text in usernames please)
7. Change the Address option to the address of the server you're connecting to, I usually host the server sometimes on `93.123.252.235:1337`
8. Enable the Connected option
9. Press T to chat, type /help in chat for a list of available commands

### Hosting a server
1. Forward the required port (1337 by default)
2. Download the [server](https://github.com/cgytrus/CatsAreOnline/releases)
3. Run CatsAreOnlineServer.exe through Powershell or cmd if you're on Windows or CatsAreOnlineServer through a terminal if you're on Linux

## Contributing
1. Clone the repository
2. Follow the [Plugin development](https://bepinex.github.io/bepinex_docs/master/articles/dev_guide/plugin_tutorial/1_setup.html)
   guide on the BepInEx wiki starting from Gathering DLL dependencies
   (look at [the project file](./CatsAreOnline/CatsAreOnline.csproj) in the first <ItemGroup> tag for a list of DLLs you need,
   you don't have to copy them in a separate folder in the project directory,
   Lidgren.Network is already installed as a NuGet library)
   
## TODO
*Entries ending with a question mark is stuff i may or may not do later*
- entity interpolation?
- sync cat features (tail, face etc.)?
- server list?
- permission system?
- server commands without having to open the client and join the server (aka server console)
