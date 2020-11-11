This is a Windows service I slaped together to read data out of my floor heating unit, and write that date to InfluxDB.
I use grafana on top of that to display the statistics for the heatpump.

It is not very configurable outside of changing the code and building it yourself. I might rebuild it someday for .NET Core and 
give it some more configurable options.

## Luxtronic 2 Documentation
The documentation for the luxtronic was not easy to find, and I managed to dig up a webpage where someone had figured it out using wireshark.
That webpage is included in the docs folder for referance if anyone wants to have a go at making something themselves. 
Some of the descriptions are a little bit strange since it was translated from German to English with google translate.