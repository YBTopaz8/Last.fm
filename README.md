Last.fm - MAUI
============

Implementation of the Last.fm API for .NET Standard 2.0 or above.

## Features

- Supports all available API endpoints (album, artist, chart, geo, library, tag, track, user).
- Supports authenticated requests.
- Supports scrobbling.

More information about the Last.fm webservice can be found [here](https://www.last.fm/api/intro).

## Getting Started
You may Created a static method to it all up for quick dev!

```
   public static void SetupLastFM()
   {
       AuthData.SetAPIData(LASTFM_API_KEY, LASTFM_API_SECRET);

       // Step 1: Define the factory method
       Func<string, string, IWebProxy, LastfmClient> factoryMethod = (apiKey, apiSecret, proxy) =>
       {
           return new LastfmClient(apiKey, apiSecret, proxy); // Replace with your LastfmClient's actual constructor or leave it like this.
       };

       // Step 2: Configure the client
       LastfmClient.Configure(factoryMethod,YOUR_LASTFM_API_KEY, YOUR_LASTFM_API_SECRET);

       //then login       
        LogInToLastFMWebsite();
   }

   
    public static void LogInToLastFMWebsite()
    {   
        _ = LastfmClient.Instance.AuthenticateAsync(LASTFM_USERNAME, LASTFM_PASSWORD);        
    }

```


```

To get started, create an instance of the `LastfmClient` class and select an API endpoint, for example to search for tracks similar to a given track:

```c#




// You need an API key to make requests to the Last.fm API.
var client = new LastfmClient(LASTFM_API_KEY);

// Find similar tracks.
var tracks = await client.Track.GetSimilarAsync(track, artist);

// Search Track
LastfmClient.Instance.Track.SearchAsync(SongTitle,ArtistName);

//Love a track on LastFM
LastfmClient.Instance.Track.LoveAsync(Title,ArtistName);

//Scrobble

        Scrobble scr = new()
        {
            Artist = string.IsNullOrEmpty(ArtistName) ? string.Empty : ArtistName,
            Track = string.IsNullOrEmpty(Title) ? string.Empty : Title,
            Album = string.IsNullOrEmpty(AlbumName) ? string.Empty : AlbumName,
            Date = DateTime.Now - TimeSpan.FromSeconds(120) // this is = time last fm will know user scrobbled song. 
        };
        _ = LastfmClient.Instance.Track.ScrobbleAsync(scr); // use _ = if you don't need th scrobble response. if you do, then do var response =
```

For more details, take a look at the [Hqub.Lastfm.Client](https://github.com/avatar29A/Last.fm/tree/master/src/Hqub.Lastfm.Client) example project.
