*OMEconomy for OpenSim (Payment Provider www.virwox.com)*
Version : Simulator-Version 0.9.0

## Using the Open Metaverse Currency
  [Follow me](Docs/Register.md)

## Installation of the Modules

Please get the OMEconomy zip archive for your OpenSimulator version from the folder _compiled/_. Extract the containing files to the root directory of OpenSimulator.
<!--
You can either use the precompiled dlls from _Binaries/_ in this archive and put it into the bin/ directory of your OpenSimulator instance or compile the source on your own. If you decide to use the precompiled ones you can skip the next section.
-->

### Installation

To enable the currency system you have to modify your OpenSim.ini configuration file and add the following lines:

    [OpenMetaverseEconomy]
      enabled = true
      OMEconomyInitialize =    "https://www.virwox.com:419/OSMoneyGateway/init.php"
      GridShortName =          "GridShortName"
      OMBaseEnvironment =      "TEST"
      OMCurrencyEnvironment =  "TEST"

The parameter "GridShortName" (_e.g._ OSGrid) is a unique identifier for your grid - please choose it carefully because it can not be changed any more.

## Mono certificates

To enable trusted https connections you must install CA certificates for mono. Please see the [Mono SecurityFAQ](http://www.mono-project.com/docs/faq/security/) for further information about why this is required.

To install the certificates type

   `mozroots --import --ask-remove`
  
  on your command line. This must be done as the user that will run OpenSimulator.exe. Alternatively you can install the certificates system wide:

    sudo mozroots --machine --import -ask-remove

## Start

Restart your OpenSimulator and check for success in the logfile _OpenSim.log_. To verify that your region is OMC-enabled please check your logs (_OpenSim.log_) and search for the entries

    13:01:50 - [PLUGINS]: Plugin Loaded: OMBaseModule
    13:01:50 - [PLUGINS]: Plugin Loaded: OMCurrencyModule
    13:01:50 - [REGIONMODULES]: From plugin OMBaseModule, (version 0.1), loaded 1 modules, 1 shared, 0 non-shared 0 unknown
    13:01:50 - [REGIONMODULES]: From plugin OMCurrencyModule, (version 0.1), loaded 1 modules, 1 shared, 0 non-shared 0 unknown
    ...
    13:01:50 - [OMECONOMY]: getGatewayURL(https://www.virwox.com:419/OS

If the currency service is NOT available or you can not find any *[OMBASE]* or *[OMECONOMY]* entries, or your simulator does not even start please read this tutorial again and follow the steps carefully.
   
### Register

Next you have to register your grid with the gateway by executing "OMRegister" at the simulator's command prompt. You are requested for your grids name and the admin avatar's UUID. If the registration process succeeds your are provided with a link to be put into a simple prim object.

    Region (region_name) # OMRegister
    [OMECONOMY]: +-
    [OMECONOMY]: | Your grid identifier is "GridShortName"
    [OMECONOMY]: | Please enter the grid's full name: GridShortName
    [OMECONOMY]: | Please enter the avatarUUID of the grid's admin: 123
    [OMECONOMY]: +-
    [OMECONOMY]: | Please visit
    [OMECONOMY]: |   https://virwox.com/OSMoneyGateway/4.0.3/Public/index.php/OMHelp/Basics/Script?name=GridShortName
    [OMECONOMY]: | to get the Terminal's script
    [OMECONOMY]: +-
    Region (region_name) #

If all steps succeeded you can register your avatars using this [Script](./InworldScripts/OSgrid_TerminalScript.lsl). Change the first line of the script to match your grid's identifier, _e.g._

    string  GRIDNAME = "OSGrid";

and put it into a prim box. After the script started the object changes it's shape to a green V.

### Switch to the Productive Environment

After successfully testing the Open Metaverse Currency with toy money you can easily switch to the productive system that supports real money. The dlls for test and productive system are the same but you have to modify the [OpenMetaverseEconomy] section in the file OpenSim.ini and restart your servers.

    [OpenMetaverseEconomy]
      enabled = true
      OMEconomyInitialize =    "https://www.virwox.com:419/OSMoneyGateway/init.php"
      GridShortName =          "GridShortName"
      OMBaseEnvironment =      "LIVE"
      OMCurrencyEnvironment =  "LIVE"

Further you have to change the line

`string SERVER = "https://www.virwox.com:8001/OS_atmint.php?grid="; // test system`

to

`string SERVER = "https://www.virwox.com:419/OS_atmint.php?grid="; // production system`

in the registration-terminal-script. 

After that you have to register with the productive environment by executing "OMRegister" again.

To actually use the OMC with real money in your grid we have to manually add it to the system. To do so please send an email to _omc@iicm.edu_ and provide your GridShortName. Finally, your grid’s avatars have to register again with VirWoX’ productive system by clicking onto the registration terminal with the modified script.

### Compile the Sources

Clone the repository and copy the contents of  _addon-modules/OMEconomy/_ into _addon-modules/_ of your OpenSimulator root directory and recompile OpenSimulator (for Linux use `runprebuild.sh` and `nant`('xbuild')).
<!--
Change the configuration files _OMEconomy/prebuild.OMBase.xml_ and _OMEconomy/prebuild.OMCurrency.xml_ according to your OpenSimulator Version (either SEVEN\_THREE for OpenSimulator 0.7.3 or SEVEN\_FOUR for OpenSimulator 0.7.4)
-->
This yields in two files _bin/OMEconomy.OMBase.dll_ and _bin/OMEconomy.OMCurrency.dll_ in root directory of your OpenSimulator instance.


* For OpenSimulator 0.7.6.3 please use the code from branch [opensim-v0.7.6.3](https://github.com/OpenMetaverseEconomy/OMEconomy-Modules/tree/opensim-v0.7.6.3).
* For OpenSimulator 0.8.1 please use the code from branch [opensim-v0.8.1](https://github.com/OpenMetaverseEconomy/OMEconomy-Modules/tree/opensim-v0.8.1).
* For OpenSimulator 0.8.2 please use the code from branch [opensim-v0.8.2](https://github.com/OpenMetaverseEconomy/OMEconomy-Modules/tree/opensim-v0.8.2).

