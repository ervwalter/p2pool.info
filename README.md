## p2pool.info

This is the source for the p2pool.info website.

## Deploying on Azure

This is designed to be hosted in Windows Azure, but can probably be easily hosted on a normal windows server as well.

1. Setup a new SQL Azure database and import the schema and data from [here](https://github.com/ervwalter/p2pool.info-data).
2. Setup a new Azure Web Site, and put it in Standard mode (this app uses too much RAM to be run in Free or Shared mode).
3. Setup git deployment for the new web site
4. Go to the configuration and add the connection string for the database you created above.  Also add an AuthToken configuration varable with whatever token you want to use.
5. Push the code to the Azure git repository with git


## License

This is open source with the MIT license:

Copyright (c) 2013 Erv Walter

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
