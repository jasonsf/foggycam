# 📹 FoggyCam - Nest Camera Recorder

![FoggyCam Logo](/media/foggycam-logo.png)

A tool to locally capture your own Nest camera stream. You can read more about my approach in the [recently published blog post](https://den.dev/blog/free-nest-video-recording/). This approach supersedes the previous implementation that relied on the `/get_image` API and instead captures the H.264 stream directly through the Nest WebSocket service.

## Configuration

To get the project to work, edit [`camera_config.json`](/foggycam/camera_config.json).

| Setting | Description |
|:--------|:------------|
| `ffmpeg_path` | Local path to the FFMPEG executable. You can [download FFMPEG](https://ffmpeg.org/download.html) for free. |
| `issue_token` | TThe full URL to the `iframerpc` network call when logging in to https://home.nest.com. You can capture it through your browser. |
| `api_key` | If you have a `x-goog-api-key` value from existing network calls, use it here. Otherwise, skip the value. |
| `user_agent` | The user agent for your web browser. |
| `cookie` | The value of the cookie from the response to the `iframerpc` network call when logging in to https://home.nest.com. You can capture it through your browser. |

## Running the application

Open `foggycam.sln` in Visual Studio and build the application. In the long run, I will make sure to make this easier and remove the need to use Visual Studio.

## OS Support

| Operating System | Status |
|:-----------------|:-------|
| Windows          | ✅ Tested & supported |
| macOS            | 🙋‍♂️ Untested |
| Linux            | 🙋‍♂️ Untested |

## Feedback

Use [issues](https://github.com/dend/foggycam/issues) or [ping me on Twitter](https://twitter.com/denniscode).