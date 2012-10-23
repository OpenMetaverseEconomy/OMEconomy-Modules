; ***** OMEconomy for OpenSim (PayManager www.virwox.com) ****
;
;Orginal Project Page : http://forge.opensimulator.org/gf/project/omeconomy
;Developer : Michael Erwin Steuer
;Code-Version : 0.03.0003
;
;Version : Prebuild, Simulator-Version 0.7.4
;Start : 2011-10-26 - Pixel Tomsen (chk) (pixel.tomsen [at] gridnet.info)
;
;git-Source: https://github.com/PixelTomsen/omeconomy-module
;

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

todo :
- copy folder addon-modules to source-folder-of-opensim
- run prebuild
- run compile/xbuild

-----------------------------------------------------------------------------------------
mono hint
startup-exception:  Invalid certificate received from server (mono issue for missing certificates) 

Linux
#shell: sudo mozroots --import --sync

or

#shell: mozroots --import --sync
-----------------------------------------------------------------------------------------

:Add following Lines to OpenSim.ini:


[OpenMetaverseEconomy]
  ;# {Enabled} {} {Enable OMEconomy} {true false} false
  enabled = true
  ;;
  OMEconomyInitialize = "https://www.virwox.com:419/OSMoneyGateway/init.php"
  ;;
  ;;Test System
  ; OMBaseEnvironment = "TEST"
  ; OMCurrencyEnvironment = "TEST"
  ;;
  ;;Productive System
  OMBaseEnvironment = "LIVE"
  OMCurrencyEnvironment = "LIVE"
