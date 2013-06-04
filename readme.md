PopFree: Open Source C# MIME Parser and Simple POP3 Client for .Net and Mono
============================================================================

PopFree is a stream-based MIME parser and threaded POP3 client written in pure C# available under MIT license. PopFree comes with a demo program called DumpPop which uses the PopFree library to download the contents of POP3 mailbox to disk with attachments stored in as separate files. The POP3 client supports SSL connections.

The MIME parser uses a subclass of System.Net.Mail.MailMessage called PopFree.Mime.RecievedMessage. RecievedMessage adds a concept of received headers. This is necessary because there are valid headers which aren't supported by the headers collection in MailMessage.

TODO
-----
- Microsoft TNEF support
- POP3 STARTTLS

Legal
-----
Copyright (C) 2010-2013 WolfeReiter, LLC [http://www.wolfereiter.com]

The MIT License [http://opensource.org/licenses/MIT]

Copyright (c) 2010-2013 WolfeReiter, LLC

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