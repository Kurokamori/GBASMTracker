# GB ASM Metrics
This is a simple project that runs in VSCode or Notepad++ to provide metrics and QOL features for Gameboy Assembly. Created to work with pret's PokeRed and PokeCrystal, but likely works with any Gameboy Assembly project.

## Installation
In the 'Latest Release' you will find the binaries for VSCode and Notepad++.

### VSCode
1. Download the vsix file
2. Open VSCode
3. Navigate to your extensions tab (for me it's the squares on the left; by default ctrl+shift+x opens it)
4. In the top right corner of the panel click the '...'
5. Select 'install from VSIX'
6. Navigate to the downloaded file and select it
7. It should now be installed and ready to use

### Notepad++
1. Download the .dll for either x64 or x32 depending on your Notepad++ type (should be the same as your system)
2. Open Notepad++
3. Navigate to Plugins > Open Plugin Folder
4. Create a folder called exactly "GBZ80AsmMetric"
5. Move the downloaded .dll file into the folder
6. Restart Notepad++ if it was open
7. The plugin should now be available under Plugins > GBZ80AsmMetrics


## Usage
Both plugins provide essentially the same functionality, but there are differences in activation and interfacing with the plugins based on the IDE.

### VSCode
With the plugin enabled, it should automatically recognize any .asm .s .inc files and show metrics on them
You can click 'ctrl + shift + m' to add a 'start' to any line which begins the counting from there (press the same key chord again on that line to disable it)
Hovering over a directive will show further information about it such as opcode, flags, etc.

### Notepad++
Once you've installed the plugin, you can access it under Plugins > GBZ80AsmMetrics
There are many options available. 'Toggle Metric' will toggle the metric view for you, and should persist once it's enabled.

NOTE : The Notepad++ plugin *will not work* if the file is not saved as .asm .s .inc it cannot otherwise recognize it.

Furthermore, in that dropdown you'll see 'Toggle Metrics Panel' which will open the metrics panel, showing all the same content as the hover action in the VSCode extension, as I couldn't do hover. You'll have to open it manually each time you open the editor. I'm hoping to fix this eventually. </br>
Also in the dropdown you can find the set start line / clear start line etc. there are also the shortcuts for them.

## Future Features / Requesting Features
There are more features planned for these plugins, and I'll continue to update them as much as I can, especially to ensure they continue working on newer versions of the IDEs.

If you notice any bugs or issues, do not hesitate to open an issue or contact me on **discord : Kurokamori**. I will do my best to be prompt in resolving any issues. (Especially as I'm not the best at counting bytes or cycles so while I did my best to test and check it there might be issues.)

If you have any feature requests, feel free to open an issue on the github or contact me directly on **discord : Kurokamori**. (Please write in the openning message why you're DMing me as I get a TON of spam)

(While this project was created for PokeRed and PokeCrystal, feel free to bring to my attention any features or bugs to do with other Gameboy Assembly projects.)
