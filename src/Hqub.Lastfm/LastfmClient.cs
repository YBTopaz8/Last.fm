﻿
namespace Hqub.Lastfm;

using Hqub.Lastfm.Cache;
using Hqub.Lastfm.Services;

using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// Last.fm client.
/// </summary>
public class LastfmClient
{
    private static LastfmClient _instance;
    private static readonly object _lock = new object();
    private static Func<LastfmClient> _factory; // Factory for initialization

    private static readonly Lazy<Version> version = new Lazy<Version>(() => Assembly.GetExecutingAssembly().GetName().Version);

    private HttpClient client;
    private IWebProxy proxy;

    #region Services

    /// <summary>
    /// The Last.fm <see cref="IAlbumService"/>.
    /// </summary>
    public readonly IAlbumService Album;

    /// <summary>
    /// The Last.fm <see cref="IArtistService"/>.
    /// </summary>
    public readonly IArtistService Artist;

    /// <summary>
    /// The Last.fm <see cref="IChartService"/>.
    /// </summary>
    public readonly IChartService Chart;

    /// <summary>
    /// The Last.fm <see cref="IGeoService"/>.
    /// </summary>
    public readonly IGeoService Geo;

    /// <summary>
    /// The Last.fm <see cref="ILibraryService"/>.
    /// </summary>
    public readonly ILibraryService Library;

    /// <summary>
    /// The Last.fm <see cref="ITagService"/>.
    /// </summary>
    public readonly ITagService Tag;

    /// <summary>
    /// The Last.fm <see cref="ITrackService"/>.
    /// </summary>
    public readonly ITrackService Track;

    /// <summary>
    /// The Last.fm <see cref="IUserService"/>.
    /// </summary>
    public readonly IUserService User;

    #endregion

    /// <summary>
    /// Gets the version of this assembly.
    /// </summary>
    public static Version Version { get { return version.Value; } }

    /// <summary>
    /// Gets the user-agent string.
    /// </summary>
    public static string UserAgent { get { return "Hqub.Lastfm/2.0"; } }

    /// <summary>
    /// Gets or sets the language to return a biography in (ISO 639 alpha-2 code).
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Gets the last.fm client session.
    /// </summary>
    public Session Session { get; private set; }

    /// <summary>
    /// Gets or sets the <see cref="IWebProxy"/> to be used in making all the calls to last.fm.
    /// </summary>
    public IWebProxy Proxy
    {
        get { return proxy; }
        set { proxy = value; ConfigurationChanged(proxy); }
    }

    /// <summary>
    /// Gets or sets the <see cref="IRequestCache"/>.
    /// </summary>
    public IRequestCache Cache { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="IScrobbleCache"/>.
    /// </summary>
    public IScrobbleCache ScrobbleCache { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LastfmClient"/> class.
    /// </summary>
    /// <param name="apiKey">The last.fm API key.</param>
    public LastfmClient(string apiKey)
        : this(apiKey, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LastfmClient"/> class.
    /// </summary>
    /// <param name="apiKey">The last.fm API key.</param>
    /// <param name="apiSecret">The last.fm API secret.</param>
    public LastfmClient(string apiKey, string apiSecret)
        : this(apiKey, apiSecret, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LastfmClient"/> class.
    /// </summary>
    /// <param name="apiKey">The last.fm API key.</param>
    /// <param name="proxy">The <see cref="IWebProxy"/> to be used for web requests.</param>
    public LastfmClient(string apiKey, IWebProxy proxy)
        : this(apiKey, null, proxy)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LastfmClient"/> class.
    /// </summary>
    /// <param name="apiKey">The last.fm API key.</param>
    /// <param name="apiSecret">The last.fm API secret.</param>
    /// <param name="proxy">The <see cref="IWebProxy"/> to be used for web requests.</param>
    public LastfmClient(string apiKey, string apiSecret, IWebProxy proxy)
    {
        Session = new Session(apiKey, apiSecret);

        this.proxy = proxy;

        Album = new AlbumService(this);
        Artist = new ArtistService(this);
        Chart = new ChartService(this);
        Geo = new GeoService(this);
        Library = new LibraryService(this);
        Tag = new TagService(this);
        Track = new TrackService(this, new ScrobbleManager(this));
        User = new UserService(this);

        // Create the HTTP client.
        ConfigurationChanged();
    }

    #region Authentication


    /// <summary>
    /// Authenticate the client <see cref="Session"/> using a username and a password.
    /// </summary>
    /// <param name="username">The user name.</param>
    /// <param name="password">The plain text password.</param>
    /// <remarks>
    /// See https://www.last.fm/api/mobileauth
    /// </remarks>
    public async Task AuthenticateAsync(string username, string password)
    {
        var request = CreateRequest("auth.getMobileSession");

        request.Parameters["username"] = username; //when you get user email and password from ui, pass here to save in their acc
        request.Parameters["password"] = password;// when you get user email and password from ui, pass here to save in their acc

        request.Sign();

        var doc = await request.PostAsync();

        Session.SessionKey = doc.Root.Element("session").Element("key").Value;
    }

    // Web authentication token.
    string token;

    /// <summary>
    /// Returns the url for web authentication.
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    /// <see cref="AuthenticateViaWebAsync"/> should be called once the user is done.
    /// </remarks>
    public async Task<string> GetWebAuthenticationUrlAsync()
    {
        var request = CreateRequest("auth.getToken");

        var doc = await request.PostAsync();

        token = doc.Root.Element("token").Value;

        return Utilities.LASTFM_SECURE + "api/auth/?api_key=" + Session.ApiKey + "&token=" + token;
    }

    /// <summary>
    /// Complete the web authentication.
    /// </summary>
    public async Task AuthenticateViaWebAsync()
    {
        if (token is null)
        {
            throw new NullReferenceException("Token is Null, Authorize App First");
        }
        var request = CreateRequest("auth.getSession");

        request.Parameters["token"] = token;

        request.Sign();

        var doc = await request.PostAsync();

        token = null;

        Session.SessionKey = doc.Root.Element("session").Element("key").Value;
    }

    #endregion

    internal Request CreateRequest(string method)
    {
        return new Request(method, client, Session, Cache);
    }

    private void ConfigurationChanged(IWebProxy proxy = null, bool automaticDecompression = true)
    {
        var handler = new HttpClientHandler();

        if (proxy != null)
        {
            handler.Proxy = proxy;
            handler.UseProxy = true;
        }

        if (automaticDecompression)
        {
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        }

        client = new HttpClient(handler);

        client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        //client.DefaultRequestHeaders.ExpectContinue = false;
    }
    /// <summary>
    /// Sets the factory method to create the singleton instance.
    /// </summary>
    public static void Configure(Func<string, string, IWebProxy, LastfmClient> factoryMethod, string apiKey, string apiSecret, IWebProxy proxy = null)
    {
        lock (_lock)
        {
            if (_instance != null)
            {
                throw new InvalidOperationException("LastfmClient has already been configured and initialized.");
            }

            _factory = () => factoryMethod(apiKey, apiSecret, proxy);
        }
    }

    /// <summary>
    /// Gets the singleton instance of LastfmClient.
    /// </summary>
    public static LastfmClient Instance
    {
        get
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    if (_factory == null)
                    {
                        throw new InvalidOperationException("LastfmClient has not been configured. Call Configure() before accessing the instance.");
                    }

                    // Use the factory method to create the instance
                    _instance = _factory();
                }

                return _instance;
            }
        }
    }

    private void ConfigurationChanged()
    {
        var handler = new HttpClientHandler();

        if (proxy != null)
        {
            handler.Proxy = proxy;
            handler.UseProxy = true;
        }

        handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

        client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", "Hqub.Lastfm/2.0");
    }
}


// LAST FM SECTION

public class AuthData
{
    // Add your credentials for testing or use the command line args.
    static string TEST_API_KEY = string.Empty;
    static string TEST_API_SECRET = string.Empty;

    public string ApiKey { get; set; }

    public string ApiSecret { get; set; }

    public string User { get; set; }

    public string Password { get; set; }

    public string SessionKey { get; set; }

    public void Print()
    {
        Console.WriteLine("API key    : {0}", ApiKey);

        if (!string.IsNullOrEmpty(ApiSecret))
        {
            Console.WriteLine("API secret : {0}", ApiSecret);
        }

        if (!string.IsNullOrEmpty(SessionKey))
        {
            Console.WriteLine("Session key: {0}", SessionKey);
        }

        if (!string.IsNullOrEmpty(User))
        {
            Console.WriteLine("User       : {0}", User);
        }

        if (!string.IsNullOrEmpty(User))
        {
            Console.WriteLine("Password   : {0}", Password);
        }
    }

    public static bool Validate(AuthData data, bool userAuth = false)
    {


        if (string.IsNullOrEmpty(data.ApiKey))
        {
            return false;
        }

        if (!userAuth)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(data.ApiSecret))
        {
            return true;
        }
        return !string.IsNullOrEmpty(data.User) && !string.IsNullOrEmpty(data.Password);
    }


    public static AuthData SetAPIData(string apiKey, string apiSecret)
    {
        return new AuthData()
        {
            ApiKey = apiKey,
            ApiSecret = apiSecret
        };
    }
    public static AuthData SetUNameAndUPass(string user, string password)
    {
        return new AuthData()
        {
            User = user,
            Password = password
        };
    }

    public static AuthData Create(string[] args)
    {
        var auth = new AuthData()
        {
            ApiKey = TEST_API_KEY,
            ApiSecret = TEST_API_SECRET
        };

        int length = args.Length;

        for (int i = 0; i < length; i++)
        {
            string s = args[i];

            if (s == "-u" || s == "--user")
            {
                if (i < length - 1)
                    auth.User = args[++i];
            }
            else if (s == "-p" || s == "--password")
            {
                if (i < length - 1)
                    auth.Password = args[++i];
            }
            else if (s == "-k" || s == "--api-key")
            {
                if (i < length - 1)
                    auth.ApiKey = args[++i];
            }
            else if (s == "-s" || s == "--api-secret")
            {
                if (i < length - 1)
                    auth.ApiSecret = args[++i];
            }
            else if (s == "-sk" || s == "--session-key")
            {
                if (i < length - 1)
                    auth.SessionKey = args[++i];
            }
        }

        return auth;
    }
}