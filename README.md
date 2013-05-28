*OMEconomy for OpenSim (Payment Provider www.virwox.com)*  
Developer : Michael Erwin Steuer  
Version : Prebuild, Simulator-Version 0.7.5  
Git-Start: 2011-10-26 - Pixel Tomsen (https://github.com/PixelTomsen/omeconomy-module)  
new git public repo 2012-10-25  
source: https://github.com/OpenMetaverseEconomy/OMEconomy-Modules   

****************************************************************************
THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY EXPRESS OR
IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE
GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER
IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*****************************************************************************


## Using the Open Metaverse Currency
  [Follow me](Docs/Register.md)

## Installation of the Modules

Please get the OMEconomy zip archive with the latest source from here. You can either use the precompiled dlls from _Binaries/_ in this archive and put it into the bin/ directory of your OpenSimulator instance or compile the source on your own. If you decide to use the precompiled ones you can skip the next section.

### Compile the Sources

Extract all files from the zip archive and put _OMEconomy/_ into _addon-modules/_ of your OpenSimulator root directory and recompile OpenSimulator (for Linux use `runprebuild.sh` and `nant`).
<!--
Change the configuration files _OMEconomy/prebuild.OMBase.xml_ and _OMEconomy/prebuild.OMCurrency.xml_ according to your OpenSimulator Version (either SEVEN\_THREE for OpenSimulator 0.7.3 or SEVEN\_FOUR for OpenSimulator 0.7.4)
-->
This yields in two files _bin/OMEconomy.OMBase.dll_ and _bin/OMEconomy.OMCurrency.dll_ in root directory of your OpenSimulator instance.

### Installation

To enable the currency system you have to modify your OpenSim.ini configuration file and add the following lines:

    [OpenMetaverseEconomy]
      OMEconomyInitialize =    "https://www.virwox.com:419/OSMoneyGateway/init.php"
      GridShortName =          "GridShortName"
      OMBaseEnvironment =      "TEST"
      OMCurrencyEnvironment =  "TEST"

The parameter "GridShortName" (_e.g._ OSGrid) is a unique identifier for your grid - please choose it carefully because it can not be changed any more.

Restart your OpenSimulator and check for success in the logfile _OpenSim.log_. To verify that your region is OMC-enabled please check your logs (_OpenSim.log_) and search for the entries

    [MODULES]: [OMBase]: Loading Shared Module.
    [MODULES]: Found Module Library [/OpenSimulatorRoot/OMBase.dll]
    [MODULES]: [OMBase]: Loading Shared Module.
    ...
    [OMBASE]:   GatewayURL: http://virwox.com:419...

If the currency service is NOT available or you can not find any *[OMBASE]* entries, or your simulator does not even start please read this tutorial again and follow the steps carefully.
When receiving the Exception: "Invalid certificate received from server (mono issue for missing certificates)" you should follow the [Mono SecurityFAQ](http://www.mono-project.com/FAQ:_Security) and either execute `sudo mozroots --import --sync` or `mozroots --import --sync` on your command line.

Next you have to register your grid with the gateway by executing "OMRegister" at the simulator's command prompt. You are requested for your grids name and the admin avatar's UUID. If the registration process succeeds your are provided with a link to be put into a simple prim object.

    Region (region_name) # OMRegister
    [OMECONOMY]: +-
    [OMECONOMY]: | Your grid identifier is "GridShortName"
    [OMECONOMY]: | Please enter the grid's full name: GridShortName
    [OMECONOMY]: | Please enter the avatarUUID of the grid's admin: 123
    [OMECONOMY]: +-
    [OMECONOMY]: | Please visit
    [OMECONOMY]: |   http://129.27.200.58/OSMoneyGateway/4.0.3/Public/index.php
                         /OMHelp/Basics/Script?name=GridShortName
    [OMECONOMY]: | to get the Terminal's script
    [OMECONOMY]: +-
    Region (region_name) #

If all steps succeeded you can register your avatars using this [Script](./Script.lsl). Change the first line of the script to match your grid's identifier, _e.g._

    string  GRIDNAME = "OSGrid";

and put it into a prim box. After the script started the object changes it's shape to a green V.

### Switch to the Productive Environment

After successfully testing the Open Metaverse Currency with toy money you can easily switch to the productive system that supports real money. The dlls for test and productive system are the same but you have to modify the [OpenMetaverseEconomy] section in the file OpenSim.ini and restart your servers.

    [OpenMetaverseEconomy]
      OMEconomyInitialize =    "https://www.virwox.com:419/OSMoneyGateway/init.php"
      GridShortName =          "GridShortName"
      OMBaseEnvironment =      "LIVE"
      OMCurrencyEnvironment =  "LIVE"

Further you have to change the line

`string SERVER = "https://www.virwox.com:8001/OS_atmint.php?grid="; // test system`

to

`string SERVER = "https://www.virwox.com:419/OS_atmint.php?grid="; // production system`

in the registration-terminal-script. To actually use the OMC with real money in your grid we have to manually add it to the system. To do so please send an email to _michael.steurer@iicm.tugraz.at_ and provide the parameters gridID, gridName, gridNickname. Finally, your grid’s avatars have to register again with VirWoX’ productive system by clicking onto the registration terminal with the modified script.