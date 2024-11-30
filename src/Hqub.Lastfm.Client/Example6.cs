
namespace Hqub.Lastfm.Client
{
    using Hqub.Lastfm.Entities;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class Example6
    {
        public static async Task Run(LastfmClient client, AuthData auth)
        {
            if (!client.Session.Authenticated)
            {
                await client.AuthenticateAsync(auth.User, auth.Password);
            }


            var trackss = await client.Track.SearchAsync("Baby Queen", "Gorillaz");
            var track = trackss[0];
            var artist = track.Artist.Name;
            var trackInfo = await client.Track.GetInfoAsync(track.Name, artist);

            Scrobble scr = new Scrobble()
            {
                Artist = artist,
                Track = track.Name,
                Album = trackInfo.Album.Name,
                Duration = trackInfo.Duration,
                Date = DateTime.Now - TimeSpan.FromMinutes(10)
            };
            List<Scrobble> scrs = new();
            scrs.Add(scr);

            var ress = await client.Track.ScrobbleAsync(scrs);
            Console.WriteLine("accepted = {0}, ignored = {1}", ress.Accepted, ress.Ignored);

            var res = await client.Track.LoveAsync(track.Name, artist);
            Console.WriteLine($"Is loved {res}");
            var l = await client.Track.GetInfoAsync(track.Name, artist);

            Console.WriteLine($"User loves song {l.UserLoved}");

            //var scrobbles = new List<Scrobble>
            //{
            //    // Fail reason: unknown artist.
            //    new Scrobble()
            //    {
            //        Artist = "Blackwave",
            //        Track = "A-okay",
            //        Date = DateTime.Now - TimeSpan.FromMinutes(10)
            //    },

            //    // Fail reason: time stamp too far in the past.
            //    new Scrobble()
            //    {
            //        Artist = "Queen",
            //        Track = "Somebody to Love",
            //        Date = DateTime.Now - TimeSpan.FromDays(15)
            //    }
            //};

            //Console.Write("Scrobbling {0} tracks: ", scrobbles.Count);

            //// Both requests will fail for different reasons. Unfortunately the Last.fm
            //// API won't give a hint what went wrong ...

            //var response = await client.Track.ScrobbleAsync(scrobbles);
            //var res = await client.Track.LoveAsync("A-okay", "Blackwave");
            //var ress = await client.Track.LoveAsync("Baby Queen", "Gorillaz");

            //await Example3.Run(client, "Baby Queen", "Gorillaz");
            //Console.WriteLine("accepted = {0}, ignored = {1}", ress.Accepted, ress.Ignored);
        }
    }
}
