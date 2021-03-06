# fiphwinfo
Information Display for Logitech Flight Instrument Panel for HWInfo

![Logitech Flight Instrument Panel with Elgato Stream Deck](https://i.imgur.com/1ATZSOj.jpg)

Use the right rotary encoder to scroll vertically.

Use the left rotary encoder to show another card.
Also, the S5 button shows the next card and the S6 button shows the previous card.

Any data from [HWInfo](https://www.hwinfo.com) can be displayed.

When HWInfo64 is detected, all the available sensors will be written at startup to the data\hwinfo.json file.

The HWINFO.inc file must be modified, to configure what will be displayed on the screen.
The HWINFO.inc file has the same format as used by various [rainmeter](https://www.deviantart.com/pul53dr1v3r/art/Rainformer-2-9-3-HWiNFO-Edition-Rainmeter-789616481) skins.

Note that you don't need to install rainmeter or any rainmeter plugin.

A configuration tool, to link sensor ids to variables in the HWINFO.inc file, can be downloaded from the hwinfo website [here](https://www.hwinfo.com/beta/HWiNFOSharedMemoryViewer.exe.7z) :

![hwinfo tool](https://i.imgur.com/Px6jvw4.png)

The HWINFO sensor data can optionally be sent to an MQTT server, by creating a file called mqtt.config (this file doesn't exist by default)

```
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <mqtt>
    <add key="mqttURI" value="192.168.2.34" />
    <add key="mqttUser" value="mqttusername" />
    <add key="mqttPassword" value="secretpassword" />
    <add key="mqttPort" value="1883" />
    <add key="mqttSecure" value="False" />
  </mqtt>
</configuration>
```

![MQTT](https://i.imgur.com/X8IkHPg.png)

A sound is played when menu options are selected.
This sound can be changed or disabled by editing the 'clickSound' key in in appsettings.config

![Screenshot 27](https://i.imgur.com/oXVakhB.png)
![Screenshot 28](https://i.imgur.com/zR9ye3a.png)

Works with these 64 bit Logitech Flight Instrument Panel Drivers (currently not with older saitek drivers) :

https://support.logi.com/hc/en-us/articles/360024848713--Downloads-Flight-Instrument-Panel

Software Version: 8.0.134.0
Last Update: 2018-01-05
64-bit

https://download01.logi.com/web/ftp/pub/techsupport/simulation/Flight_Instrument_Panel_x64_Drivers_8.0.134.0.exe

Thanks to :

https://github.com/jdahlblom/DCSFIPS

https://www.hwinfo.com/

