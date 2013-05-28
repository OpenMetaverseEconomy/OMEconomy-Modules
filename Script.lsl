string  GRIDNAME = "";

// constants:
string  VERSION = "10"; // this is sent on every request
string  SERVER = "https://www.virwox.com:8001/OS_atmint.php?grid=";  // test system
//string  SERVER = "https://www.virwox.com:419/OS_atmint.php?grid=";  // live system

integer TIMEOUT = 60;  // set HTTP request timeout to this many seconds
string  PARAMETER_SEP = ",";   // separator used for separating parameters when talking to server
string  INSTRUCTION_SEP = ";"; // separator used for separating instructions in server response
string  LIST_SEP = "|";// separator used for separating list elements
integer DEBUG = FALSE; // set this to TRUE for debug output
integer SHOW_STATUS = TRUE;// do this only on ATMs

integer STATUS_FACE  = 3;
vector GREEN = <0.0,0.547,0.508>;

// global variables:
string  agentName;   // name of agent we are currently engaged with
string  agentID; // ID of agent we are currently engaged with
key requestID;   // ID of currently outstanding Dataserver request
integer timeout; // number of seconds until the next "PING" (or other action) to the server
string  token;   // token to be sent on every HTTP request as first parameter
float   startTimeSecs;   // time we started the last HTTP request (just the seconds; for calculating lastRoundTrip)
float   lastRoundTrip;   // elapsed time of last round trip to server (logged at server for statistics & tuning)
integer requestPending;  // if true we are waiting for an HTTP response
string  dialogName;  // name of dialog (for disambiguity of button response);
integer dialogChannel;   // dialog input channel.
integer dialogHandle;// handle of dialog listener
integer listenHandle;// handle of chat listener
string  confirmString;   // server wants us to pass back this string on the next request
listgetList; // list of object parameters to get
string  tportSim;// simulator to teleport to
vector  tportPos;// teleport position

// global function for debugging purposes
debug(string s)
{
  if (DEBUG)
  llOwnerSay(s+" Mem="+(string)llGetFreeMemory());
//llOwnerSay(s);
}

// initialisation work
init()
{
  debug("init");
//  llSetStatus(STATUS_BLOCK_GRAB, TRUE);// dont allow people to move us around.
  requestPending = FALSE;
  dialogChannel=-126584;   // whatever as long as its != 0
  tportSim = "";
  llSetTexture(TEXTURE_BLANK, ALL_SIDES);
  sendRequest(["INIT", llDumpList2String(
   llParcelMediaQuery([PARCEL_MEDIA_COMMAND_TEXTURE,PARCEL_MEDIA_COMMAND_URL]), LIST_SEP)]);
}

// send HTTP request to the server
sendRequest(list parameters)
{
  string request = llDumpList2String(parameters, PARAMETER_SEP);
  if (request == "PING")
if (confirmString != "")// we have to confirm something instead of just PINGing:
  request = "CONFIRM" + PARAMETER_SEP + confirmString;

  debug("sending request: "+request);
  llSetTimerEvent(TIMEOUT);
  startTimeSecs = (float)llGetSubString(llGetTimestamp(), 17, -2);
  llHTTPRequest(SERVER + GRIDNAME, [HTTP_METHOD, "POST", HTTP_MIMETYPE, "text/xml", HTTP_VERIFY_CERT, FALSE ],
VERSION + PARAMETER_SEP + token + PARAMETER_SEP + (string)lastRoundTrip + PARAMETER_SEP + request);

  if (SHOW_STATUS && request != "PING")
  {
debug("data transfer");
llSetColor(<1,1,0>, STATUS_FACE);  // yellow
  }

  llSetTimerEvent(TIMEOUT);// HTTP timeout value
  requestPending = TRUE;
}

// process HTTP response from server
integer processResponse(key id, integer status, list meta, string body)
{
  requestPending = FALSE;
  getList = [];

  // calculate time for last round trip (60 seconds max.):
  float now = (float)llGetSubString(llGetTimestamp(), 17, -2);
  lastRoundTrip = now - startTimeSecs;
  if (lastRoundTrip < 0)
lastRoundTrip += 60.0;

  if (status != 200) // do not check for matching requestid, as we run a stateless protocol!
return FALSE;// some error has occurred

  // The whole instruction list, including parameters:
  list instructions = llParseStringKeepNulls(body, [INSTRUCTION_SEP], []);

  while (llGetListLength(instructions))// process the whole list
  {
// the first instruction (with parameters):
list param = [];
param = llParseStringKeepNulls(llList2String(instructions,0), [PARAMETER_SEP], []); // 1st instruction

// the name of the method to execute:
string method = llList2String(param, 0);
if (method == "TOKEN") // change token
{
  token = llList2String(param, 1);
  jump break;
}
if (method == "TIMEOUT")  // set timeout for next ping to x seconds
{
  llSetTimerEvent(llList2Integer(param, 1));   // check server connection after this time
  jump break;
}
if (method == "TEXTURE")  // set texture of arbitrary face on this prim
{
  llSetTexture(llList2String(param,1), llList2Integer(param,2));
  jump break;
}
if (method == "ALPHA")// set transparency of this object
{
  llSetAlpha(llList2Float(param,1), llList2Integer(param,2));
  jump break;
}
if (method == "PARAM")// set parameters of this object
{
  string parameters = llUnescapeURL(llList2String(param,1));
  debug(parameters);
  llSetPrimitiveParams(string_2_list(parameters));
  jump break;
}
if (method == "MEDIA")// set media parameters
{
  llParcelMediaCommandList(string_2_list(llUnescapeURL(llList2String(param,1))));
  jump break;
}
if (method == "OMEGA")// set target omega (rotation speed)
{
  llTargetOmega((vector)llUnescapeURL(llList2String(param,1)), llList2Float(param,2), 0.1);
  jump break;
}
if (method == "SOUND")// play sound
{
  llPlaySound(llList2String(param,1), llList2Float(param,2));
  jump break;
}
if (method == "LOADURL")  // send user to URL
{
  llLoadURL(llList2Key(param,1), llUnescapeURL(llList2String(param,2)),
 llUnescapeURL(llList2String(param,3)));
  jump break;
}
if (method == "DIALOG")   // open user dialog
{
  dialogChannel = llList2Integer(param,- 1);// last parameter is channel number
  dialogName = llList2String(param, -2);// the one before is the dialog name
  llListenRemove(dialogHandle); // remove old channel, if any
  dialogHandle = llListen(dialogChannel, "", agentID,"");   // listen to events on dialogChannel from this agent only
  llDialog(llList2Key(param,1), llUnescapeURL(llList2String(param,2)),
   llList2List(param, 3, -3), dialogChannel);
  jump break;
}
if (method == "AGENTDATA")// request Agent Data
{
  agentID = llList2Key(param,1);
  integer hypergrid = llList2Integer(param,3);   // 1 if this is a hypergrid agent => cannot ask dataserver at the moment
  if (hypergrid)
sendRequest(["DATA", agentID, agentName, ""]);   // send empty data
  else
requestID = llRequestAgentData(agentID, llList2Integer(param,2));
  debug((string)requestID);
  jump break;
}
if (method == "IM")   // send IM to user (2 sec penalty!)
{
  llInstantMessage(llList2Key(param,1), llUnescapeURL(llList2String(param,2)));
  jump break;
}
if (method == "SAY")  // speak on public channel
{
  llSay(0, llUnescapeURL(llList2String(param,1)));
  jump break;
}
if (method == "TELEPORT") // set Teleport address
{
  tportSim = llUnescapeURL(llList2String(param,1));
  tportPos = (vector)llUnescapeURL(llList2String(param,2));
  jump break;
}
if (method == "ANIM") // Texture Animation
{
  llSetTextureAnim(llList2Integer(param,1), llList2Integer(param,2), llList2Integer(param,3), llList2Integer(param,4),
   llList2Float(param,5), llList2Float(param,6), llList2Float(param,7));
  jump break;
}
if (method == "CONFIRM")  // server requests me to send back a string on the next request
{
  confirmString = llList2String(param,1);
  jump break;
}
if (method == "ENDCONFIRM")// server has received confirmation; stop sending this string
{
  confirmString = "";
  jump break;
}
if (method == "LISTEN")   // listen to arbitrary channel
{
  llListenRemove(listenHandle); // remove old channel, if any
  listenHandle = llListen(llList2Integer(param,1), "", NULL_KEY,"");
  jump break;
}
if (method == "GET")  // get parameters of this object
{
  getList = llGetPrimitiveParams(string_2_list(llUnescapeURL(llList2String(param,1))));
  jump break;
}
if (method == "PARTICLE")  // set particle effect parameters
{
  llParticleSystem(string_2_list(llUnescapeURL(llList2String(param,1))));
  jump break;
}
if (method == "SETPIN")// set PIN for remote software update
{
  llSetRemoteScriptAccessPin(llList2Integer(param,1));
  jump break;
}
if (method == "NAME") // set name and description of object
{
  llSetObjectName(llUnescapeURL(llList2String(param,1)));
  llSetObjectDesc(llUnescapeURL(llList2String(param,2)));
  jump break;
}
if (method == "SETTEXT")  // set text (and color) above object
{
  llSetText(llUnescapeURL(llList2String(param,1)),(vector)llUnescapeURL(llList2String(param,2)),llList2Float(param,3));
  jump break;
}
if (method == "XCPT") // server exception
  return FALSE;// report failure
if (method == "RESET")   // reset Script
  llResetScript();

@break;// simulate a break statement
debug(method + "+" + (string)(llGetListLength(param)-1) + "parameters");

instructions = llDeleteSubList(instructions, 0, 0);   // remove first instruction from the list
  }
  return TRUE;   // success
}

////////////////////////////////////////////////////////
// snip list convert // ready jack // 11 Nov 2005 // 1.0
// this decode a string into a list.

list string_2_list(string s) {
list l = [];
list result = [];
l = llParseStringKeepNulls(s, [LIST_SEP], []);
integer len = llGetListLength(l);
integer i;
for (i = 0; i < len; ++i) {
integer type = (integer) llList2String(l, i);
++i; // CLEVERNESS WARNING...BE VERY AFRAID
if (type == TYPE_INTEGER) result += (integer)llList2String(l, i);
else if (type == TYPE_FLOAT) result += (float)llList2String(l, i);
else if (type == TYPE_KEY) result += (key)llList2String(l, i);
else if (type == TYPE_VECTOR) result += (vector)llList2String(l, i);
else if (type == TYPE_ROTATION) result += (rotation)llList2String(l, i);
else result += llList2String(l, i);
}
debug(s);
return result;
}

///////////////////////////////////////////////////////////////////////////////////////////////////////////////
// STATE MACHINE
//

// we start in this state. Only used for initialization work
default
{
  state_entry()
  {
init();
state running;
  }

  on_rez(integer start_param)
  {
llResetScript(); // resets owner and all variables
  }
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////
// this is the default state while the Script is running and listening to user events
state running
{
  state_entry()
  {
if (SHOW_STATUS)
{
  llSetColor(GREEN, STATUS_FACE);  // green
}
llSetTimerEvent(TIMEOUT); // check server connection after this time
debug("running");
  }

  on_rez(integer start_param)
  {
llResetScript(); // resets owner and all variables
  }

  timer()
  {
if (requestPending)// timeout waiting for HTTP response
{
  debug("timeout waiting for server");
  state disconnected;  //   problem
}
else   // just the ordinary PING timer
  sendRequest(["PING"]);   //   dont leave running state on pings
  }

  // process response from server
  http_response(key id, integer status, list meta, string body)
  {
if (!processResponse(id, status, meta, body))
{// some error has occurred (Exception?)
  state disconnected;// display "out of order"
}
if (SHOW_STATUS)
{
  llSetColor(GREEN, STATUS_FACE);  // green
}
if (getList != [])   // we have something to report to server
  sendRequest(["PARAM", llEscapeURL(llDumpList2String(getList, LIST_SEP))]);
  }

  // process response from dataserver
  dataserver(key queryid, string data)
  {
debug("RESPONSE FROM DATASERVER: "+data);
sendRequest(["DATA", agentID, agentName, llEscapeURL(data)]);   // forward to server
  }

  // user has touched us
  touch_start(integer num_detected)
  {
agentID = llDetectedKey(0);
agentName = llDetectedName(0);
sendRequest(["TOUCH", agentID, agentName, llDetectedLinkNumber(0)]);
if (tportSim)
  llMapDestination(tportSim, tportPos, tportPos); // and offer teleport to user
  }

  // process dialog response from user
  listen(integer chan, string name, key id, string message)
  {
debug(name + " on channel " + (string)chan + "says: " + message);
if (chan == dialogChannel && dialogName != "ignore")   // this is just FYI, no need to send response
{
  agentID = id;
  agentName = name;
  sendRequest(["DLG_MSG", agentID, agentName, dialogName, llEscapeURL(message)]);  // pass on to server
  llListenRemove(dialogHandle);
}
if (chan == 0 && llStringLength(message) > 8)
{
  sendRequest(["CHAT", id, name, llEscapeURL(message)]);   // public chat (for language detection)
  llListenRemove(listenHandle);
}
  }
}


// an error has occured during communication with server (most likely a timeout) - try to reconnect
// while in this state we double timeout on every failure (up to 10 minutes), to reduce SIM load
state disconnected
{
  state_entry()
  {
debug("disconnected");
if (SHOW_STATUS)
{
  llSetColor(<1,0,0>, STATUS_FACE);  // red
}
timeout = TIMEOUT;// initial timeout
sendRequest(["PING"]);
  }

  on_rez(integer start_param)
  {
debug("disconnected: on_rez("+(string)start_param+")");
timeout = TIMEOUT;// initial timeout
sendRequest(["PING"]);
  }

  // process response from server
  http_response(key id, integer status, list meta, string body)
  {
if (SHOW_STATUS)
{
//  llSetTexture(TEX_DISCONNECTED, ALL_SIDES);
  llSetColor(<1,0,0>, STATUS_FACE);  // red
}
if (processResponse(id, status, meta, body))   // success!!
  state running;// this will also reset the timer
  }

  // force an immediate try to reconnect if touched:
  touch_start(integer num_detected)
  {
sendRequest(["PING"]);
  }

  // timeout
  timer()
  {
timeout = timeout * 2;
if (timeout > 600)
  timeout = 600;

debug("timeout - retry in "+(string)timeout + " seconds.");
sendRequest(["PING"]);
llSetTimerEvent(timeout); // this modifies the timout value of the HTTP request as well
  }

  state_exit()
  {
timeout = TIMEOUT;  // reset to initial value
debug("connected");
  }
}