# Current Version 1.0
https://github.com/Russell-KV4S/KV4S.AR.DSTAR.ircDBB.StationTracking/releases/download/v1.0/KV4S.AR.DSTAR.ircDBB.StationTracking.zip

# KV4S.AR.DSTAR.ircDBB.StationTracking
KV4S.AR.DSTAR.ircDBB.StationTracking gives you ability to get email notifications about reflector changes of your favorite D-Star stations.
The program works in conjuction with data from this site: https://www.openquad.net/last.php

Note: this only tracks stations coming through ircDBB not on the traditional ICOM repeater stack. If you are interested in that as well check out this project by Bill (AB4EJ) and myself:
https://github.com/AB4EJ-1/DWatcherV1 

Contact me if you have feature request or use Git and create your enhancements and merge them back in.

I recommend using Windows Task Scheduler to kick the program off on about a 5-10 minute interval.

Once you download, edit the .config file that's along side the executable as needed (you won't need to copy the config on future releases unless there is a structure change). 
There are comments in the file that tells you how to format the entries. Here is the example file:
```
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
    <startup> 
        <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.5.2" />
    </startup>
    <appSettings>
        <!--use commas with no spaces to add more-->
        <add key="Callsigns" value="KV4S"/>
        <!--"Y" or "N" values-->
        <!--If you run this as a job or don't need to see the output then make Unattended Yes-->
        <add key="Unattended" value="N"/>
        <add key="EmailError" value="Y"/>
        <add key="StatusEmails" value="Y"/>
        <!--Enter Value in Minutes-->
        <add key="MinutesUntilNextNotification" value="60"/>
      
      <!--Email Parameters - Gmail example-->
      <!--use commas with no spaces to add more emails to the email To and From field-->
      <add key="EmailTo" value="example@gmail.com"/>
      <add key="EmailFrom" value="example@gmail.com"/>
      <add key="SMTPHost" value="smtp.gmail.com"/>
      <add key="SMTPPort" value="587"/>
      <add key="SMTPUser" value="example@gmail.com"/>
      <add key="SMTPPassword" value="Password"/>
    </appSettings>
</configuration>

```
Errors will be logged to an ErrorLog.txt 
