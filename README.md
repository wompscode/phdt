# phdt
**PH**oebe's **D**isk **T**ester  
*heads up*: On large volumes, it will use a lot of CPU because lots of Tasks are created. I should probably limit how many tasks run at once.
  
You should probably just use [h2testw](https://h2testw.org/). I just got bored and wanted to make something, and I got an SSD from CeX, so I had an excuse to make this. This does not tell you read/write speed, and only will tell you if your drive corrupts stuff at a certain point.  
  
Creates a dummy file of a specified size, clones it a bunch of times to fit a specified size, and then checks every file to see if they match the dummy file. If one doesn't, it'll stop and tell you that it doesn't match anymore and at what point it doesn't match.