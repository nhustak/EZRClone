# EZRClone

A Windows desktop GUI for [rclone](https://rclone.org/), the command-line cloud storage sync tool.

## Features

- **Config Management** — View and manage rclone remote configurations
- **Job Management** — Create, edit, and run sync/copy jobs with customizable options (transfers, checkers, bandwidth limits, filters)
- **Remote Browser** — Browse files and directories on any configured remote with Explorer-style navigation
- **Search** — Find files across remotes using wildcard patterns (e.g., `*.jpg`, `backup*`)
- **Download/Delete** — Download files to local storage or delete from remotes (supports multi-select)
- **Directory Info** — Get recursive file counts and total sizes for directories
- **Batch Import** — Import multiple jobs from text files

## Requirements

- Windows 10/11
- .NET 9.0 Runtime
- [rclone](https://rclone.org/downloads/) installed and configured

## Getting Started

1. Download and install rclone
2. Configure at least one remote using `rclone config`
3. Launch EZRClone
4. Go to Settings and set the path to `rclone.exe`
5. Start browsing your remotes or create sync jobs

## Building from Source

```bash
dotnet build
```

## License

MIT License

Copyright (c) 2024

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
