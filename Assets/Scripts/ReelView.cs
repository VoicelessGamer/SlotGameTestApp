using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UnityEngine.UI;
using System.IO;
using Newtonsoft.Json;

public class ReelView : MonoBehaviour {

    public OutputManager outputManager;
    public GameObject baseSymbol;

    public float symbolGap;
    public List<Color> colourRange;

    private Vector3 symbolStartPosition;

    //<gameId, <configId, <reel, <symbol positions>>>>
    private Dictionary<string, Dictionary<string, List<List<Vector2Int>>>> parsedReelConfigurations;

    //map of symbol id to colour (filled)
    private Dictionary<int, Color> symbolColours;

    private JToken viewJson;
    private string gameId = "";

    private List<JToken> reelObjects;
    private int rscIndex;

    private List<GameObject> symbols;

    private float reelViewWidth;
    private float reelViewHeight;
    private float baseSymbolWidth;
    private float baseSymbolHeight;

    private void Awake() {
        reelViewWidth = ((RectTransform)transform).rect.width;
        reelViewHeight = ((RectTransform)transform).rect.height;

        baseSymbolWidth = ((RectTransform)baseSymbol.transform).rect.width;
        baseSymbolHeight = ((RectTransform)baseSymbol.transform).rect.height;

        //getting the top left position of this object as an offset
        symbolStartPosition = new Vector3((float)-((reelViewWidth * 0.5) - (baseSymbolWidth * 0.5)), (float)((reelViewHeight * 0.5) - (baseSymbolHeight * 0.5)), 0);

        parseReelConfigurations();
        symbolColours = new Dictionary<int, Color>();
        symbols = new List<GameObject>();

        reelObjects = new List<JToken>();
    }

    public void setViewJson(JToken jToken, string gId) {
        viewJson = jToken;
        gameId = gId;

        reelObjects = new List<JToken>();

        if (viewJson.SelectToken("irs") == null) {
            //start/update/end response
            reelObjects.AddRange(viewJson.SelectToken("rsc").Children());

            //TODO: Check if rsc is missing then parse as dutch game
        } else {
            //init response
            reelObjects.Add(viewJson.SelectToken("irs"));
        }

        rscIndex = reelObjects.Count - 1;

        //check for no reel objects (meaning buy a bonus etc.)
        if (rscIndex < 0) {
            return;
        }

        //setup last reel set in list
        setupSymbols(reelObjects[rscIndex].SelectToken("reels"));
    }

    public void onOpenWebsocketConnection() {
        //on a new connection attempt destroy all symbols in view
        destroySymbols();
    }

    private void parseReelConfigurations() {
        //<gameId, <configId, <reel, <symbol positions>>>>
        parsedReelConfigurations = new Dictionary<string, Dictionary<string, List<List<Vector2Int>>>>();

        JObject reelConfigurations;

        //retrieve the reel configurations from the json stored in the streaming assets folder
        //streaming assets files can change after game has been built
        try {
            reelConfigurations = JObject.Parse(File.ReadAllText(Application.streamingAssetsPath + "/Json/ReelConfigurations.json"));
        } catch (JsonReaderException ex) {
            outputManager.queueLogEntry("Error parsing ReelConfigurations.json", ex.Message, LogEntryDetails.EntryType.ERROR, null);
            return;
        }

        /**
         * Example:
         * {
	            "bs077_dls": {
		            "default": [
			            [[0,1],[0,2],[0,3]],
			            [[1,1],[1,2],[1,3]],
			            [[2,1],[2,2],[2,3]],
			            [[3,1],[3,2],[3,3]],
			            [[4,1],[4,2],[4,3]]
		            ]
	            }
            }
         **/

        //iterates the individual games in the json { "bs077_dls" }
        foreach (JProperty game in reelConfigurations.Children<JProperty>()) {
            Dictionary<string, List<List<Vector2Int>>> configMap = new Dictionary<string, List<List<Vector2Int>>>();

            //iterates the individual objects within the current game { "default" }
            foreach (JProperty configuration in game.Value) {
                List<List<Vector2Int>> config = new List<List<Vector2Int>>();

                //iterate each reel for current configuration { '[[0,1],[0,2],[0,3]]' }
                foreach (JToken reel in configuration.Value) {
                    List<Vector2Int> reelPositions = new List<Vector2Int>();

                    //convert each position to a vector 2 { '[0,1]' }
                    foreach (JToken position in reel.Children()) {
                        reelPositions.Add(new Vector2Int((int)position.First, (int)position.Last));
                    }

                    config.Add(reelPositions);
                }

                configMap.Add(configuration.Name, config);
            }

            parsedReelConfigurations.Add(game.Name, configMap);
        }
    }

    public void setupSymbols(JToken currentReels) {

        destroySymbols();

        Dictionary<int, List<int>> parsedReelMap = new Dictionary<int, List<int>>();

        //iterates the list of reels in the current rsc object
        foreach (JToken reel in currentReels.Children()) {
            List<int> symbolIds = new List<int>();
            //iterate the symbol ids placing them into an integer list
            foreach (JToken symbolId in reel.SelectToken("si").Children()) {
                symbolIds.Add((int) symbolId);
            }

            //add the list of symbols to a map with the reel id as the key
            parsedReelMap.Add((int)reel.SelectToken("id"), symbolIds);
        }

        //setup the symbol on screen
        if(parsedReelConfigurations.ContainsKey(gameId)) {
            setupWithConfiguration(parsedReelMap);
        } else {
            setupWithoutConfiguration(parsedReelMap);
        }
    }

    private void setupWithConfiguration(Dictionary<int, List<int>> parsedReelMap) {

        //retrieve just default configuration for now (implement individual feature configs later)
        List<List<Vector2Int>> reelPositions = parsedReelConfigurations[gameId]["default"];

        float reelWidth = 0f;
        float reelHeight = 0f;

        for (int i = 0; i < reelPositions.Count; i++) {
            List<Vector2Int> reel = reelPositions[i];
            for (int j = 0; j < reel.Count; j++) {
                Vector2Int symbolPoint = reel[j];

                if(symbolPoint.x == -1 || symbolPoint.y == -1) {
                    continue;
                }

                if(!parsedReelMap.ContainsKey(symbolPoint.x) || symbolPoint.y >= parsedReelMap[symbolPoint.x].Count) {
                    continue;
                }

                //instantiate a symbol object
                Vector3 newPos = new Vector3(symbolStartPosition.x + (baseSymbolWidth * i) + (i * symbolGap), symbolStartPosition.y - ((baseSymbolHeight * j) + (j * symbolGap)), symbolStartPosition.z);
                GameObject symbol = createSymbol(newPos);

                //updating current width and height for scaling purposes
                reelWidth = newPos.x - symbolStartPosition.x + baseSymbolWidth > reelWidth ? newPos.x - symbolStartPosition.x + baseSymbolWidth : reelWidth;
                reelHeight = symbolStartPosition.y - newPos.y + baseSymbolHeight > reelHeight ? symbolStartPosition.y - newPos.y + baseSymbolHeight : reelHeight;

                //add symbol to list to be destroyed later
                symbols.Add(symbol);

                //use the symbol point from the reel layout to get the symbol id to be displayed
                int symbolId = parsedReelMap[symbolPoint.x][symbolPoint.y];

                //update the symbol text to display the symbol id
                Text symbolText = (Text)symbol.GetComponentInChildren(typeof(Text));
                symbolText.text = symbolId.ToString();

                //update the symbol image colour to make it easier to differentiate (add ability for proper images later)
                Image symbolImage = (Image)symbol.GetComponent(typeof(Image));
                if (!symbolColours.ContainsKey(symbolId)) {
                    symbolColours.Add(symbolId, generateColour(symbolId));
                }
                symbolImage.color = symbolColours[symbolId];
            }
        }

        scaleReelView(reelWidth, reelHeight);
    }

    private void setupWithoutConfiguration(Dictionary<int, List<int>> parsedReelMap) {

        float reelWidth = 0f;
        float reelHeight = 0f;

        //just iterate over the symbols sent from server and display as is
        foreach (KeyValuePair<int, List<int>> entry in parsedReelMap) {
            List<int> reel = entry.Value;
            for (int j = 0; j < reel.Count; j++) {
                //instantiate a symbol object
                Vector3 newPos = new Vector3(symbolStartPosition.x + (baseSymbolWidth * entry.Key) + (entry.Key * symbolGap), symbolStartPosition.y - ((baseSymbolHeight * j) + (j * symbolGap)), symbolStartPosition.z);
                GameObject symbol = createSymbol(newPos);

                //updating current width and height for scaling purposes
                reelWidth = newPos.x - symbolStartPosition.x + baseSymbolWidth > reelWidth ? newPos.x - symbolStartPosition.x + baseSymbolWidth : reelWidth;
                reelHeight = symbolStartPosition.y - newPos.y + baseSymbolHeight > reelHeight ? symbolStartPosition.y - newPos.y + baseSymbolHeight : reelHeight;

                //add symbol to list to be destroyed later
                symbols.Add(symbol);

                //get the symbol id to be displayed
                int symbolId = reel[j];

                //update the symbol text to display the symbol id
                Text symbolText = (Text)symbol.GetComponentInChildren(typeof(Text));
                symbolText.text = symbolId.ToString();

                //update the symbol image colour to make it easier to differentiate (TODO: Add ability for proper images later)
                Image symbolImage = (Image)symbol.GetComponent(typeof(Image));
                if (!symbolColours.ContainsKey(symbolId)) {
                    symbolColours.Add(symbolId, generateColour(symbolId));
                }
                symbolImage.color = symbolColours[symbolId];
            }
        }

        scaleReelView(reelWidth, reelHeight);
    }

    private GameObject createSymbol(Vector3 position) {
        //instantiate creates a new instance of the provided game object
        GameObject symbol = Instantiate(baseSymbol, position, Quaternion.identity);
        //sets the parent of the created symbol to this object. Second parameter, if true,
        //the parent-relative position, scale and rotation are modified such that the object keeps the same world space position, rotation and scale as before.
        symbol.transform.SetParent(transform, false);
        return symbol;
    }

    private void scaleReelView(float reelWidth, float reelHeight) {
        //if reel width larger than reel view width then get new scale otherwise scale remains at 1
        float widthScale = reelWidth > reelViewWidth ? reelViewWidth / reelWidth : 1;
        //if reel height larger than reel view height then get new scale otherwise scale remains at 1
        float heightScale = reelHeight > reelViewHeight ? reelViewHeight / reelHeight : 1;

        if(widthScale == 1 && heightScale == 1) {
            //both scales == 1, reset scale
            transform.localScale = new Vector3(1, 1, 1);
        } else if(widthScale < heightScale) {
            //width scale is smallest, use this as the uniform scale
            transform.localScale = new Vector3(widthScale, widthScale, 1);
        } else {
            //height scale is smallest, use this as the uniform scale
            transform.localScale = new Vector3(heightScale, heightScale, 1);
        }
    }

    public Color generateColour(int symbolId) {
        if(symbolId >= 0) {
            if(symbolId < colourRange.Count) {
                //use the predefined colour for the symbol id given
                return colourRange[symbolId];
            } else {
                //generate random colour
                return new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), 1f);
            }
        } else {
            //-1 used in reel expansion/cascade games, always use grey
            return new Color(0.6f, 0.6f, 0.6f, 1f);
        }
    }

    private void destroySymbols() {
        //iterate the stored list of symbol game objects
        for(int i = symbols.Count - 1; i >= 0; i--) {
            GameObject current = symbols[i];
            //remove the symbol from the list
            symbols.RemoveAt(i);
            //destroy the symbol game object
            Destroy(current);
        }
    }

    public void onIncrementReelView() {
        //each reel modifier will add a set of reels to the rsc list
        //this allows you to view the next reel set in the list, providing you're not already on the last element
        if (rscIndex < reelObjects.Count - 1) {
            rscIndex++;
            setupSymbols(reelObjects[rscIndex].SelectToken("reels"));
        }
    }

    public void onDecrementReelView() {
        //each reel modifier will add a set of reels to the rsc list
        //this allows you to view the previous reel set in the list, providing you're not already on the first element
        if (rscIndex > 0) {
            rscIndex--;
            setupSymbols(reelObjects[rscIndex].SelectToken("reels"));
        }
    }

    public void onSkipToLastReelView() {
        //each reel modifier will add a set of reels to the rsc list
        //this allows you to view the last reel set in the list
        if (rscIndex < reelObjects.Count - 1) {
            rscIndex = reelObjects.Count - 1;
            setupSymbols(reelObjects[rscIndex].SelectToken("reels"));
        }
    }

    public void onSkipToFirstReelView() {
        //each reel modifier will add a set of reels to the rsc list
        //this allows you to view the first reel set in the list
        if (rscIndex > 0) {
            rscIndex = 0;
            setupSymbols(reelObjects[rscIndex].SelectToken("reels"));
        }
    }
}
