# RMT Multiplayer

## Tehnička dokumentacija projekta

**Predmet:** Računarske Mreže i Telekomunikacije
**Akademska godina:** 2025/2026
**Autori:** [Ime Prezime 1], [Ime Prezime 2]

---

## Sadržaj

1. Uvod
2. Korišćene tehnologije i verzije
3. Opšta arhitektura sistema
4. Bootstrap aplikacije i singleton sloj
5. Autentifikacija korisnika
6. Relay servis i NAT traversal
7. Transportni sloj (UTP, DTLS, WSS)
8. Lobby servis i otkrivanje sesija
9. Tok konekcije i razmena payload-a
10. Sinhronizacija stanja u igri
11. RPC pozivi i autoritativna logika
12. Gameplay sistemi kroz mrežu
13. Disconnect i oslobađanje resursa
14. WebGL build i ograničenja browser okruženja
15. Zaključak

---

## 1. Uvod

RMT Multiplayer je top-down multiplayer arkadna igra u kojoj igrači kontrolišu male tenkove, skupljaju novčiće, pucaju jedni na druge i leče se u zonama za heal. Igra je rađena u Unity okruženju, koristeći zvanični Netcode for GameObjects (NGO) framework u kombinaciji sa Unity Gaming Services (Relay, Lobby, Authentication).

Glavni motiv za izbor ove teme je bio da pokrijemo što više različitih aspekata mrežne komunikacije u jednom projektu. Igra se može pokrenuti i kao standalone Windows build i kao WebGL build u browseru, sto je zahtevalo posebnu pažnju oko transportnog protokola. Pored toga, koristimo i sloj nad transportom za otkrivanje sesija (Lobby), kao i Relay servis koji nam rešava problem NAT traversal-a bez potrebe da igrači otvaraju portove na svom ruteru.

Cilj ove dokumentacije je da objasnimo kako svaki segment mrežnog dela radi, počevši od trenutka pokretanja klijenta, preko autentifikacije, pravljenja sesije, pridruživanja drugih igrača i sinhronizacije stanja u toku same igre, pa sve do urednog gašenja konekcije.

Sav prikazani kod nalazi se u repozitorijumu projekta, u folderu `Assets/Scripts/Networking/` i `Assets/Scripts/Core/`.

---

## 2. Korišćene tehnologije i verzije

Projekat smo razvijali u **Unity 6000.3.6f1** (Unity 6), pošto smo hteli da koristimo najnoviju verziju NGO paketa. Konkretni paketi iz `Packages/manifest.json` koji su nam relevantni:

| Paket | Verzija | Namena |
|-------|---------|--------|
| com.unity.netcode.gameobjects | 2.11.0 | High-level mrežni framework |
| com.unity.services.multiplayer | 2.2.1 | Relay, Lobby, transport |
| com.unity.services.authentication | latest | Prijava korisnika |
| com.unity.multiplayer.tools | 2.2.8 | Profiler i debug alati |

NGO je apstrakcija iznad transport sloja koja nam daje gotove koncepte kao što su NetworkObject, NetworkVariable, NetworkList i RPC pozivi. Konkretno, NGO je konfigurisan tako da koristi **UnityTransport** kao implementaciju transporta. UnityTransport interno radi preko **Unity Transport Package (UTP)** koji je nizak C# layer nad UDP socketima, sa DTLS enkripcijom (ili WSS u browseru, o tome kasnije).

Bitno je razlikovati ove slojeve, pošto se često meša:

```
+---------------------------------------------+
| Game logika (TankPlayer, ProjectileLauncher)|
+---------------------------------------------+
| Netcode for GameObjects (NetworkBehaviour)  |
+---------------------------------------------+
| UnityTransport (DTLS / WSS)                 |
+---------------------------------------------+
| Unity Relay Service (NAT traversal)         |
+---------------------------------------------+
| UDP / TCP socket OS-a                       |
+---------------------------------------------+
```

---

## 3. Opšta arhitektura sistema

Igra koristi **Host-Client** model. Jedan igrač pokreće Host, što znači da je istovremeno i server i klijent u istoj instanci. Ostali se pridružuju kao čisti klijenti. Server je autoritativan, dakle on je taj koji donosi finalne odluke o stanju sveta (zdravlje, novac, pozicija novčića, ko je koga ubio).

Glavni razlog zašto smo izabrali Host-Client umesto čistog dedicated servera je trošak. Dedicated server bi zahtevao da neko hostuje server 24/7, dok ovako bilo koji igrač može da otvori sesiju. Mana je naravno da ako Host izađe, sesija se ruši, ali za potrebe projekta to je prihvatljivo.

Da bi se igrači uopšte mogli povezati međusobno preko interneta, koristimo **Unity Relay**. Relay je javno dostupan server koji prosleđuje pakete između Host-a i klijenata. Bez Relay-a bismo morali da rešavamo port forwarding na svakom ruteru posebno, što nije realno za production scenario.

Otkrivanje sesija (browsing aktivnih partija) ide preko **Unity Lobby** servisa. Lobby je u suštini server-side struktura sa listom igrača i custom metadata-om, gde je najvažnije polje **join code** koji dobijemo od Relay-a.

Šema povezivanja je sledeća:

```
 Player A (Host)              Unity Cloud                Player B
+--------------+              +----------+              +----------+
| Create Relay |------------->|  Relay   |              |          |
| Get joinCode |<-------------|          |              |          |
| Create Lobby |------------->|  Lobby   |              |          |
| Heartbeat... |              |          |<-------------| Query    |
|              |              |          |------------->| Lobbies  |
|              |              |          |              |          |
|              |              |          |<-------------| JoinByCode|
| <==========relay UDP/WSS==========================>   |          |
+--------------+              +----------+              +----------+
```

---

## 4. Bootstrap aplikacije i singleton sloj

Pri samom startu igre prvi se pokreće `ApplicationController`. On instancira dva DontDestroyOnLoad objekta, jedan za Host singleton i jedan za Client singleton. Razlog što oba postoje istovremeno je to što kod nas svaki igrač potencijalno može i da hostuje i da se priključi, pa hoćemo da oba menadžera budu spremna.

```csharp
public class ApplicationController : MonoBehaviour
{
    [SerializeField] private ClientSingleton clientPrefab;
    [SerializeField] private HostSingleton hostPrefab;

    async void Start()
    {
        DontDestroyOnLoad(gameObject);
        await LaunchInMode(SystemInfo.graphicsDeviceType ==
                          UnityEngine.Rendering.GraphicsDeviceType.Null);
    }

    private async Task LaunchInMode(bool isDedicatedServer)
    {
        if (isDedicatedServer) { /* za dedicated build */ }
        else
        {
            HostSingleton hostSingleton = Instantiate(hostPrefab);
            hostSingleton.CreateHost();

            ClientSingleton clientSingleton = Instantiate(clientPrefab);
            bool authenticated = await clientSingleton.CreateClient();

            if (authenticated)
                clientSingleton.GameManager.GoToMenu();
        }
    }
}
```

Detekcija da li je build dedicated server radi se preko `SystemInfo.graphicsDeviceType`. Ako Unity nije pronašao grafičku karticu (`Null`), pretpostavljamo da je build pokrenut u headless modu, što tipično radi server.

`HostSingleton` i `ClientSingleton` su klasične singleton implementacije sa `FindFirstObjectByType` fallback-om. Svaki od njih drži referencu na svoj GameManager (`HostGameManager` i `ClientGameManager`) koji sadrži pravu mrežnu logiku.

---

## 5. Autentifikacija korisnika

Pre nego što uopšte krenemo sa konektovanjem na Relay ili Lobby, klijent mora da bude prijavljen na Unity Services. Bez važećeg auth tokena, ovi servisi odbijaju zahteve sa HTTP 401.

Mi koristimo **anonimnu autentifikaciju**. To znači da nema lozinki niti emailova, već Unity backend generiše jedan trajni PlayerId vezan za uređaj. Taj PlayerId nam kasnije služi kao primarni ključ za identifikaciju igrača u Lobby-ju.

Sav posao je obavijen u `AuthenticationWrapper`, statičkoj klasi koja drži stanje autentifikacije kao enum:

```csharp
public enum AuthState
{
    NotAuthenticated,
    Authenticating,
    Authenticated,
    Error,
    TimeOut
}
```

Glavna funkcija ima retry logiku, pošto se zna desiti da prva HTTP konekcija ka Unity backend-u podbaci (npr. ako je internet sporiji ili je servis privremeno overloaded):

```csharp
private static async Task SignInAnonymouslyAsync(int maxTries)
{
    AuthState = AuthState.Authenticating;
    int tries = 0;
    while (AuthState == AuthState.Authenticating && tries < maxTries)
    {
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            if (AuthenticationService.Instance.IsSignedIn &&
                AuthenticationService.Instance.IsAuthorized)
            {
                AuthState = AuthState.Authenticated;
                break;
            }
        }
        catch (AuthenticationException aex) { AuthState = AuthState.Error; }
        catch (RequestFailedException rex)  { AuthState = AuthState.Error; }

        tries++;
        await Task.Delay(1000);
    }

    if (AuthState != AuthState.Authenticated)
        AuthState = AuthState.TimeOut;
}
```

Maksimalno se pokušava 5 puta, sa pauzom od 1 sekunde između pokušaja. Posle uspešne prijave, `AuthenticationService.Instance.PlayerId` postaje korisnikov jedinstveni ID koji se kasnije pakuje u payload i šalje Host-u prilikom konekcije.

---

## 6. Relay servis i NAT traversal

NAT (Network Address Translation) je razlog zašto dva kućna računara ne mogu samo tako da otvore UDP socket jedan ka drugom. Privatne IP adrese iza rutera nisu rutabilne sa interneta, pa bismo morali ili da ručno otvaramo portove ili da koristimo neku tehniku tipa STUN/TURN.

Unity Relay je u suštini hostovani TURN-like servis. Host kreira **Allocation** na Relay serveru, dobija njegovu javnu adresu i jedan kratki **join code**. Klijenti pošalju taj join code Relay-u i dobiju instrukcije kako da pošalju pakete do tog istog allocation-a. Relay onda dalje prosleđuje pakete Host-u i obrnuto.

Kod Host-a izgleda ovako:

```csharp
allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);
joinCode  = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
Debug.Log($"Join Code: {joinCode}");
```

`MaxConnections` je 20, što znači da Relay rezerviše slotove za do 20 paralelnih klijenata na ovoj sesiji.

Klijent radi obrnutu stvar. Umesto `CreateAllocationAsync`, on poziva `JoinAllocationAsync` sa kodom koji je dobio od korisnika ili iz Lobby metadata-e:

```csharp
allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
```

Posle ovoga oba imaju validan `RelayServerData` objekat koji se predaje UnityTransport-u, pa NGO može da kreće sa konekcijom kao da je u pitanju klasičan klijent-server.

Treba spomenuti i jednu sitnicu iz logova. Pri pokretanju vidimo:

```
[Multiplayer]: Could not do Qos region selection. Will use default.
QoS failed due to [PlatformNotSupportedException].
```

Ovo je samo informativna poruka iz QoS SDK-a koji bi inače pingovao različite Unity regione i izabrao najmanju latenciju. Pošto na WebGL platformi QoS nije podržan, sistem se vraća na default region. Ne utiče na funkcionalnost.

---

## 7. Transportni sloj (UTP, DTLS, WSS)

Sada nešto malo dublje o transportu, pošto je ovo deo gde smo dosta naučili. UnityTransport je transport plugin za NGO. On može da radi preko više protokola, a najbitniji za nas su:

- **UDP / DTLS** za standalone build (Windows, Mac, Linux)
- **WSS (Secure WebSocket)** za WebGL build

DTLS (Datagram Transport Layer Security) je u suštini TLS preko UDP-a. Zadržava brzinu i nepouzdanost UDP-a, ali šifrira sadržaj i autentifikuje server. Latencija je odlična zato što nema TCP handshake-a niti retransmisije na nivou transporta, a NGO ima sopstveni reliability sloj za pakete koje zaista moraju da stignu.

Problem je što browser ne dozvoljava aplikaciji da otvori sirov UDP socket. WebGL build može da otvori samo WebSocket konekciju (TCP ispod), i to obavezno preko `wss://` ako je sama stranica servirana preko HTTPS-a. Zbog toga smo morali da uvedemo preprocessor granu:

```csharp
UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

#if UNITY_WEBGL && !UNITY_EDITOR
    RelayServerData relayServerData =
        AllocationUtils.ToRelayServerData(allocation, "wss");
    transport.UseWebSockets = true;
#else
    RelayServerData relayServerData =
        AllocationUtils.ToRelayServerData(allocation, "dtls");
#endif

transport.SetRelayServerData(relayServerData);
```

`#if UNITY_WEBGL && !UNITY_EDITOR` znači da Editor uvek koristi DTLS, čak i ako je aktivni build target WebGL, što nam olakšava testiranje. U Editoru DTLS lepo radi i možemo brzo iterirati.

Bitna stvar koju smo naučili je da Host i Client moraju da koriste isti tip Relay konekcije. Ako Host kreira allocation kao `dtls`, a klijent pokuša da se konektuje kao `wss`, Relay će odbiti konekciju jer su to dva različita endpoint-a. To znači da za pravu cross-platform podršku (Windows hostuje, WebGL se pridružuje) moramo i Host-a da prebacimo na `wss`, sto malo poveća latenciju ali radi i u browseru i u Windows-u.

---

## 8. Lobby servis i otkrivanje sesija

Relay nam rešava prenos paketa, ali ne i pitanje "koja je sesija aktivna i kako da je nađem". Za to koristimo Lobby servis. Lobby je strukturirani objekat na Unity backend-u koji sadrži:

- ime sesije
- maksimalan broj igrača
- listu trenutnih igrača
- proizvoljnu metadata mapu (`Dictionary<string, DataObject>`)

Najbitnije polje u metadata-i je **JoinCode**. Pošto Lobby ne zna ništa o Relay-u, mi sami pakujemo join code u metadata i tako ga vežemo za ovu konkretnu sesiju.

### Kreiranje Lobby-ja na Host strani

```csharp
Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(
    PlayerPrefs.GetString(NameSelector.PlayerNameKey, "??") + "'s Lobby",
    MaxConnections,
    new CreateLobbyOptions
    {
        IsPrivate = false,
        Data = new Dictionary<string, DataObject>
        {
            { "JoinCode",
              new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
        }
    });
lobbyId = lobby.Id;

HostSingleton.Instance.StartCoroutine(HeartbeatLobby(15));
```

`VisibilityOptions.Member` znači da `JoinCode` može da pročita samo onaj ko se pridruži lobby-ju, ne i neko ko samo proverava QueryLobbiesAsync. To je zaštita da ne neko ne uđe direktno na Relay zaobilazeći join logiku.

### Heartbeat

Lobby na backend-u ima TTL. Ako Host ne pošalje "heartbeat" duže od oko 30 sekundi, Lobby se briše. Zato smo napravili koroutinu koja svakih 15 sekundi ping-uje servis:

```csharp
private IEnumerator HeartbeatLobby(float waitTimeSeconds)
{
    WaitForSecondsRealtime delay = new WaitForSecondsRealtime(waitTimeSeconds);
    while (true)
    {
        LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
        yield return delay;
    }
}
```

### Listanje i pridruživanje

Na klijent strani imamo `LobbiesList` skriptu koja pravi QueryLobbiesAsync sa filterima. Nas zanimaju samo otvoreni lobiji u kojima ima slobodnih slotova:

```csharp
QueryLobbiesOptions options = new QueryLobbiesOptions { Count = 25 };
options.Filters = new List<QueryFilter>
{
    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots,
                    "0", QueryFilter.OpOptions.GT),
    new QueryFilter(QueryFilter.FieldOptions.IsLocked,
                    "0", QueryFilter.OpOptions.EQ)
};
QueryResponse lobbies = await LobbyService.Instance.QueryLobbiesAsync(options);
```

Klik na neki Lobby u listi okida sledeću sekvencu:

```csharp
public async void JoinAsync(Lobby lobby)
{
    Lobby joiningLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);
    string joinCode = joiningLobby.Data["JoinCode"].Value;
    await ClientSingleton.Instance.GameManager.StartClientAsync(joinCode);
}
```

Prvo se priključujemo Lobby-ju (sto nam daje pristup metadata-i), zatim izvlačimo JoinCode i tek onda kreće prava Relay konekcija.

---

## 9. Tok konekcije i razmena payload-a

Ovo je verovatno najinteresantniji deo, jer pokazuje kako se identifikacija igrača "preliva" sa Lobby/Auth sloja u NGO sloj.

NGO ima ugrađen mehanizam zvan **ConnectionApprovalCallback**. To je callback koji se okida na serveru svaki put kada novi klijent pokuša konekciju, pre nego što NGO uopšte spawnuje bilo šta. U tom callback-u imamo prilike da:

1. Pročitamo proizvoljan binarni payload koji je klijent poslao
2. Odlučimo da li ćemo konekciju da odobrimo ili odbijemo
3. Postavimo poziciju spawn-a i da li uopšte da se kreira PlayerObject

Mi koristimo payload da pošaljemo igračevo ime i auth ID:

```csharp
[Serializable]
public class UserData
{
    public string userName;
    public string userAuthId;
}
```

Kod klijenta i Host-a, pre `StartClient`/`StartHost`, payload se priprema ovako:

```csharp
UserData userData = new UserData()
{
    userName  = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "??"),
    userAuthId = AuthenticationService.Instance.PlayerId
};
string payload = JsonUtility.ToJson(userData);
byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);

NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
NetworkManager.Singleton.StartClient();
```

Dakle, serializujemo UserData u JSON, pa u UTF-8 bajtove, i taj `byte[]` postavljamo na `NetworkConfig.ConnectionData`. NGO će ga prilikom handshake-a poslati serveru.

Na server strani, callback obrađuje payload:

```csharp
private void ApprovalCheck(
    NetworkManager.ConnectionApprovalRequest request,
    NetworkManager.ConnectionApprovalResponse response)
{
    string payload = System.Text.Encoding.UTF8.GetString(request.Payload);
    UserData userData = JsonUtility.FromJson<UserData>(payload);

    clientIdToAuth[request.ClientNetworkId] = userData.userAuthId;
    authIdToUserData[userData.userAuthId]   = userData;

    response.Approved          = true;
    response.Position          = SpawnPoint.GetRandomSpawnPos();
    response.Rotation          = Quaternion.identity;
    response.CreatePlayerObject = true;
}
```

Bitna stvar je da NGO svakom klijentu dodeli interni `ClientNetworkId` (ulong), dok mi za poslove sa Lobby-jem koristimo `userAuthId` (string PlayerId). Zbog toga čuvamo dve mape:

```csharp
private Dictionary<ulong, string>    clientIdToAuth   = new();
private Dictionary<string, UserData> authIdToUserData = new();
```

Ovo nam omogućava da kasnije, recimo kada igrač izgubi konekciju, znamo i njegov NGO clientId i njegov Lobby auth ID, pa možemo da ga obrišemo i iz Lobby-ja.

---

## 10. Sinhronizacija stanja u igri

NGO nudi nekoliko mehanizama za sinhronizaciju. Mi smo koristili tri glavna:

### 10.1 NetworkVariable

`NetworkVariable<T>` je vrednost koja postoji na serveru i automatski se replikuje na sve klijente. Po default-u, samo server može da je piše, dok je svi mogu čitati. Idealno za stvari kao što su zdravlje, broj novčića, ime igrača.

Primer iz `Health.cs`:

```csharp
public class Health : NetworkBehaviour
{
    [field: SerializeField] public int MaxHealth { get; private set; } = 100;
    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        CurrentHealth.Value = MaxHealth;
    }

    private void ModifyHealth(int value)
    {
        if (isDead) return;
        int newHealth = CurrentHealth.Value + value;
        CurrentHealth.Value = Mathf.Clamp(newHealth, 0, MaxHealth);
        if (CurrentHealth.Value == 0)
        {
            OnDie?.Invoke(this);
            isDead = true;
        }
    }
}
```

Tek kada server promeni `CurrentHealth.Value`, NGO interno generiše delta paket i pošalje ga svim klijentima. Klijent samo čita ovu vrednost i koristi je za npr. health bar UI.

### 10.2 NetworkList

Za kolekcije koje rastu i opadaju koristimo `NetworkList<T>`. Element mora da bude `INetworkSerializable` struct. Mi smo to iskoristili za leaderboard:

```csharp
public struct LeaderboardEntityState
    : INetworkSerializable, IEquatable<LeaderboardEntityState>
{
    public ulong ClientId;
    public FixedString32Bytes PlayerName;
    public int Coins;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref Coins);
    }
}
```

Primetićete `FixedString32Bytes` umesto klasičnog `string`. Razlog je što NGO ne ume direktno da serializuje managed string preko mreže pošto je dužina dinamična. `FixedString32Bytes` je struct sa fiksnih 32 bajta, pa veličina paketa može da se izračuna unapred.

`NetworkList` emituje event `OnListChanged` na svim klijentima kada se nešto promeni, sto koristimo da osvežavamo UI:

```csharp
leaderboardEntities.OnListChanged += HandleLeaderboardEntitiesChanged;
```

### 10.3 RPC (Remote Procedure Call)

Za jednokratne akcije koje treba da se izvrše na drugoj strani koristimo RPC. Postoje dva tipa:

- `[ServerRpc]` se poziva sa klijenta, izvršava se na serveru
- `[ClientRpc]` se poziva sa servera, izvršava se na svim klijentima

Više o RPC-jevima u sledećem poglavlju.

---

## 11. RPC pozivi i autoritativna logika

Najlepši primer RPC obrasca u našem kodu je pucanje. Logika je da pucanj mora da bude trenutno vidljiv lokalnom igraču (instant feedback), ali pravi projektil koji nanosi štetu mora da bude autoritativan, znači živi na serveru.

```csharp
void Update()
{
    if (!IsOwner) return;

    if (timer > 0) timer -= Time.deltaTime;

    if (fireAction.WasPressedThisFrame())
    {
        if (timer > 0) return;
        if (wallet.totalCoins.Value < costToFire) return;

        PrimaryFireServerRpc();      // pravi projektil na serveru
        SpawnDummyProjectile();      // vizuelni feedback lokalno

        timer = 1 / fireRate;
    }
}
```

Server RPC trošii novac, instancira pravi projektil i obaveštava ostale klijente da nacrtaju dummy:

```csharp
[ServerRpc]
void PrimaryFireServerRpc()
{
    if (wallet.totalCoins.Value < costToFire) return;
    wallet.SpendCoins(costToFire);

    GameObject projectileInstance = Instantiate(
        serverProjectilePrefab,
        firePoint.position,
        firePoint.parent.parent.rotation,
        null);

    Physics2D.IgnoreCollision(
        playerCollider,
        projectileInstance.GetComponent<Collider2D>());

    if (projectileInstance.TryGetComponent<DealDamageOnContact>(out var dealDamage))
        dealDamage.setOwner(OwnerClientId);

    if (projectileInstance.TryGetComponent<Rigidbody2D>(out var rb))
        rb.linearVelocity = rb.transform.up * projectileSpeed;

    SpawnDummyProjectileClientRpc();
}

[ClientRpc]
void SpawnDummyProjectileClientRpc()
{
    if (IsOwner) return;     // vlasnik je vec lokalno nacrtao
    SpawnDummyProjectile();
}
```

Zašto pravimo "lažni" projektil na klijentima umesto da NGO sinhronizuje server projektil? Zato što je projektil brz i kratko živi, pa bi sinhronizacija pozicije svaki frame bila skuplja od optimističnog spawn-a sa istim parametrima. Server projektil je nevidljiv (`serverProjectilePrefab` ima drugačiji visual setup) ili se renderuje samo na serveru, a klijenti vide sopstvene dummy verzije koje vizuelno odgovaraju.

Pošto i server i klijenti znaju iste početne uslove (pozicija, rotacija, brzina), trajektorije su praktično identične. Ovo je klasična tehnika u FPS i top-down igrama, poznata kao "fire-and-forget" cosmetic projectile.

---

## 12. Gameplay sistemi kroz mrežu

### 12.1 Kretanje igrača

Kretanje radimo client-side, znači vlasnik tenka obrađuje svoj input lokalno i piše direktno u Rigidbody2D:

```csharp
public override void OnNetworkSpawn()
{
    if (!IsOwner) return;
    moveAction = InputSystem.actions.FindAction("Move");
}

private void FixedUpdate()
{
    if (!IsOwner) return;
    rb.linearVelocity = (Vector2)bodyTransform.up *
                        previousMoveInput.y * moveSpeed;
}
```

Sinhronizacija pozicije ka ostalim klijentima ide preko NGO `NetworkTransform` komponente (komponenta na prefab-u), koja periodično šalje delte pozicije i rotacije. Ovo nije strogo autoritativno (klijent može da "vara" o svojoj poziciji), ali za ovaj projekat je dovoljno i drastično pojednostavljuje kod.

### 12.2 Novčići i wallet

`CoinWallet` drži `NetworkVariable<int> totalCoins`. Kada igrač pređe preko novčića, trigger se desi na svim klijentima, ali samo server zaista uvećava balans:

```csharp
private void OnTriggerEnter2D(Collider2D other)
{
    if (!other.TryGetComponent<Coin>(out Coin coin)) return;
    int value = coin.Collect();

    if (!IsServer) return;
    totalCoins.Value += value;
}
```

Bitno je da se `Collect()` poziva i na klijentu i na serveru, ali sa različitim ponašanjem. Klijent samo sakrije sprite radi instant feedback-a, server dodatno briše NetworkObject:

```csharp
public override int Collect()
{
    if (!IsServer)
    {
        Show(false);
        return 0;
    }
    if (alreadyCollected) return 0;
    alreadyCollected = true;
    Destroy(gameObject);
    return coinValue;
}
```

Kada igrač umre, deo njegovog kapitala se baca na zemlju kao "bounty" koji drugi mogu da pokupe:

```csharp
private void HandleDie(Health health)
{
    int bountyValue = (int)(totalCoins.Value * (bountyPercentage / 100f));
    int bountyCoinValue = bountyValue / bountyCoinCount;

    if (bountyCoinValue < minBountyCoinValue) return;

    for (int i = 0; i < bountyCoinCount; i++)
    {
        BountyCoin coinInstance = Instantiate(
            coinPrefab, getSpawnPoint(), Quaternion.identity);
        coinInstance.setValue(bountyCoinCount);
        coinInstance.NetworkObject.Spawn();
    }
}
```

`NetworkObject.Spawn()` je ključan poziv koji od običnog GameObject-a pravi network entity koji NGO replicira na sve klijente.

### 12.3 Leaderboard

Leaderboard sinhronizujemo preko `NetworkList<LeaderboardEntityState>`. Server, kada se neki igrač spawn-uje, doda novi entry, i pretplati se na njegov `totalCoins.OnValueChanged`:

```csharp
private void HandlePlayerSpawned(TankPlayer player)
{
    leaderboardEntities.Add(new LeaderboardEntityState
    {
        ClientId   = player.OwnerClientId,
        PlayerName = player.PlayerName.Value,
        Coins      = 0
    });

    player.Wallet.totalCoins.OnValueChanged +=
        (oldCoins, newCoins) =>
            HandleCoinsChanged(player.OwnerClientId, newCoins);
}
```

Klijenti slušaju `OnListChanged` event i ažuriraju UI prikaz. Lista se sortira po broju novčića i prikazuje se top N, sa specijalnim slučajem da se lokalni igrač uvek prikaže čak i ako nije u top N:

```csharp
LeaderBoardEntityDisplay myDisplay = entityDisplays.FirstOrDefault(
    x => x.ClientId == NetworkManager.Singleton.LocalClientId);

if (myDisplay != null &&
    myDisplay.transform.GetSiblingIndex() >= entitiesToDisplay)
{
    leaderboardEntityHolder.GetChild(entitiesToDisplay - 1)
        .gameObject.SetActive(false);
    myDisplay.gameObject.SetActive(true);
}
```

### 12.4 Respawn

Kada igrač umre, server brise njegov network object i u sledećem frame-u kreira novi, pa ga "vraća" istom vlasniku preko `SpawnAsPlayerObject`:

```csharp
private IEnumerator RespawnPlayer(ulong ownerClientId, int keptCoins)
{
    yield return null;

    TankPlayer playerInstance = Instantiate(
        playerPrefab,
        SpawnPoint.GetRandomSpawnPos(),
        Quaternion.identity);

    playerInstance.NetworkObject.SpawnAsPlayerObject(ownerClientId);
    playerInstance.Wallet.totalCoins.Value += keptCoins;
}
```

Razlika između `Spawn` i `SpawnAsPlayerObject` je u tome što druga metoda kaže NGO-u da je ovo novi "Player object" za datog klijenta, što znači da se ovaj klijent automatski tretira kao vlasnik (IsOwner postaje true na njegovoj strani).

---

## 13. Disconnect i oslobađanje resursa

Disconnect može da se desi iz nekoliko razloga:

1. Korisnik klikne "Leave Game"
2. Korisnik zatvori klijent
3. Mreža je pukla, timeout NGO-a se okida
4. Host je sam izasao iz igre

Za prva tri slučaja sa strane klijenta, slušamo NGO event:

```csharp
public NetworkClient(NetworkManager networkManager)
{
    this.networkManager = networkManager;
    networkManager.OnClientDisconnectCallback += OnClientDisconnect;
}

private void OnClientDisconnect(ulong clientId)
{
    if (clientId != 0 && clientId != networkManager.LocalClientId) return;
    Disconnect();
}

public void Disconnect()
{
    if (SceneManager.GetActiveScene().name != MenuSceneName)
        SceneManager.LoadScene(MenuSceneName);
    if (networkManager.IsConnectedClient)
        networkManager.Shutdown();
}
```

Filter `clientId == 0 || clientId == LocalClientId` je tu zato što NGO ovaj event okida i kada se neko drugi diskonektuje, a nas zanima samo naš slučaj.

Na strani Host-a, kada neko ode, treba ga i u Lobby servisu skinuti sa liste, inače Lobby ostaje sa "duhom" igrača koji više nije online:

```csharp
private void OnClientDisconnect(ulong clientId)
{
    if (clientIdToAuth.TryGetValue(clientId, out string authId))
    {
        clientIdToAuth.Remove(clientId);
        authIdToUserData.Remove(authId);
        OnClientLeft?.Invoke(authId);
    }
}

private async void HandleClientLeft(string authId)
{
    try
    {
        await LobbyService.Instance.RemovePlayerAsync(lobbyId, authId);
    }
    catch (LobbyServiceException e) { Debug.Log(e); }
}
```

Kada Host sam zatvori sesiju, sve mora da se počisti uredno: koroutina za heartbeat se zaustavi, lobby se obriše na backend-u, NGO callbacks se odvežu i NetworkManager se gasi:

```csharp
public async void Shutdown()
{
    HostSingleton.Instance.StopCoroutine(nameof(HeartbeatLobby));
    if (!string.IsNullOrEmpty(lobbyId))
    {
        try { await LobbyService.Instance.DeleteLobbyAsync(lobbyId); }
        catch (LobbyServiceException e) { Debug.Log(e); }
        lobbyId = string.Empty;
    }

    NetworkServer.OnClientLeft -= HandleClientLeft;
    NetworkServer?.Dispose();
}
```

`Dispose` pattern smo iskoristili svuda gde čuvamo NGO callbacks, da bi se sigurno odjavili od event-a i izbegli memory leak.

---

## 14. WebGL build i ograničenja browser okruženja

Kao što smo gore pomenuli, WebGL build ne može da koristi UDP, već samo WebSocket. To podrazumeva nekoliko stvari:

1. **Latencija je nešto veća** pošto TCP ima retransmisiju i in-order delivery, što za real-time igru nije idealno.
2. **Nema UDP packet loss-a koji bi se vremenom prepravio**, ali zato kasne dropped paketi.
3. **Browser zahteva WSS (ne WS) ako je stranica preko HTTPS-a**, što je default na većini production hosting servisa.

Promene koje smo morali da napravimo:

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    RelayServerData relayServerData =
        AllocationUtils.ToRelayServerData(allocation, "wss");
    transport.UseWebSockets = true;
#else
    RelayServerData relayServerData =
        AllocationUtils.ToRelayServerData(allocation, "dtls");
#endif
```

Pored toga, u Unity Project Settings-u smo morali da podesimo i WebGL template kao i kompresiju (Brotli) jer se inače build pakuje u .br fajlove koje stari serveri ne serviraju sa pravim MIME header-om.

Što se tiče cross-play scenarija (Windows hostuje, browser se priključuje, ili obrnuto), tu je trenutno ograničenje. Pošto allocation može da bude samo jednog tipa, ako Host pravi DTLS allocation, WebGL klijent ne može da uđe. Rešenje je da Host uvek pravi WSS allocation, pa će i WebGL i Windows klijenti moći da uđu, doduše sa malo većom latencijom na Windows-u nego što bi imali sa DTLS-om.

---

## 15. Zaključak

Tokom rada na ovom projektu prošli smo kroz praktično sve glavne slojeve modernog mrežnog stack-a u game development-u. Krenuli smo od najnižeg, transportnog sloja (UDP/DTLS i WSS u browseru), kroz Relay servis koji rešava problem NAT traversal-a bez ručnog port forwarding-a, preko Lobby servisa za otkrivanje sesija, autentifikacije Unity Services-a, pa sve do high-level mehanizama Netcode for GameObjects-a (NetworkVariable, NetworkList, RPC).

Najvažnije lekcije koje smo izvukli:

- **Autoritativni server** je krucijalan za stvari koje smeju da utiču na rezultat (zdravlje, novac, smrt). Vizuelne stvari mogu da budu klijent-side optimistično za bolji feel.
- **Payload kroz ConnectionApproval** je elegantan način da se Auth identitet prenese u NGO svet pre nego što player object uopšte postoji.
- **WebGL podrška** je netrivijalna i zahteva preprocesorske grane na transport sloju.
- **Lobby heartbeat** mora da se ne zaboravi, inače sesije nestaju sa backend-a usred igre.
- **Dispose pattern** za sve mrežne callback-e sprečava bagove kada se igra restartuje.

Sve u svemu, projekat je pokazao kako iz perspektive aplikacije izgleda kombinacija UDP transport-a, TCP fallback-a (WSS), HTTP REST API-ja (Lobby, Auth) i tehnika tipa Relay servisa. Iako Unity sve ove komponente lepo umota u SDK, razumevanje šta se dešava ispod je važno, posebno kada nešto pukne i kada treba iz log-a (kao što smo imali sa QoS warning-om i DTLS/WSS greškom) razumeti zašto.

Zahvaljujemo se profesoru i asistentima na predmetu Računarske Mreže i Telekomunikacije.

---

**Kraj dokumenta**
