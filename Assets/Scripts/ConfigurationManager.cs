using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class ConfigurationManager : MonoBehaviour {
    
    public OutputManager outputManager;

    public Dropdown endpointDropdown;
    public Dropdown gameDropdown;
    public InputField playTokenInputField;
    public Dropdown stakeDropDown;
    public InputField profileIndexInputField;
    public RequestConfigurationSettings requestConfigurationSettings;

    public const int MAX_PLAY_TOKEN = 200000000;

    private string playToken = "";

    private JObject configurations;
    //stores an endpoint location to a rigging http location
    private Dictionary<string, string> locationMap;
    //stores a game name to the associated game id
    private Dictionary<string, string> games;
    //stores the list of stakes as strings
    private List<string> stakes;
    
    //Holds actions received from another Thread. Will be coped to copiedQueuedActions then executed from there
    private static List<Action> queuedActions = new List<Action>();
    //Holds Actions copied from queuedActions to be executed
    List<Action> copiedQueuedActions = new List<Action>();
    // Used to know if we have new Action function to execute. This prevents the use of the lock keyword every frame
    private volatile static bool noQueuedActions = true;

    void Awake() {
        
        locationMap = new Dictionary<string, string>();

        //retrieve the reel configurations from the json stored in the streaming assets folder
        //streaming assets files can change after game has been built
        try {
            configurations = JObject.Parse(File.ReadAllText(Application.streamingAssetsPath + "/Json/Configurations.json"));
            //setup the dropdown menus using the data from the jsons
            setupEndpointDropdown();
            setupGameDropdown();
        } catch (JsonReaderException ex) {
            outputManager.queueLogEntry("Error parsing Configurations.json", ex.Message, LogEntryDetails.EntryType.ERROR, null);
        }

        //currently limiting the player token to 25 alphanumeric characters
        //allowing letter due to being able to t pass through the currency/country codes
        playTokenInputField.characterLimit = 25;
        playTokenInputField.contentType = InputField.ContentType.Alphanumeric;

        //don't usually go past single digit profiles but to be safe set to 2
        profileIndexInputField.characterLimit = 2;
        //profile index can only be an integer number
        profileIndexInputField.contentType = InputField.ContentType.IntegerNumber;
        //auto set to profile 0 as that will work with any game that doesn't use multiple profiles
        profileIndexInputField.text = "0";
    }

    public void Update() {
        //quick return if no actions queued
        if (noQueuedActions) {
            return;
        }

        //Clear the old actions from the copiedQueuedActions queue
        copiedQueuedActions.Clear();

        //lock the queuedActions list to prevent other threads accessing
        lock (queuedActions) {
            //Copy queuedActions to the copiedQueuedActions variable
            copiedQueuedActions.AddRange(queuedActions);
            //Now clear the queuedActions after copying
            queuedActions.Clear();
            noQueuedActions = true;
        }

        // Loop and execute the functions from the copiedQueuedActions
        for (int i = 0; i < copiedQueuedActions.Count; i++) {
            copiedQueuedActions[i].Invoke();
        }
    }

    public static void executeInUpdate(Action action) {
        //require action otherwise throw exception
        if (action == null) {
            throw new ArgumentNullException("action");
        }

        //lock the queuedActions list to prevent other threads accessing
        lock (queuedActions) {
            //add the given action to the queue
            queuedActions.Add(action);
            noQueuedActions = false;
        }
    }

    public void onWebSocketOpen() {
        //called when a new websocket connection is successfully made
        
        //endpoint no longer selectable while websocket open
        endpointDropdown.interactable = false;

        //game and playtoken can now be selected
        gameDropdown.interactable = true;
        playTokenInputField.interactable = true;
    }

    public void onWebSocketClose() {
        //called when a websocket connection is closed
        
        //unlock the dropdown to be able to select the endpoint
        endpointDropdown.interactable = true;

        //lock the inputs for choosing the game and playtoken
        gameDropdown.interactable = false;
        playTokenInputField.interactable = false;

        //clears the stake dropdown values
        stakeDropDown.ClearOptions();
        stakes = new List<string>();
        //locks the stake selection
        stakeDropDown.interactable = false;

        //locks and resets the profile index input
        profileIndexInputField.text = "0";
        profileIndexInputField.interactable = false;
    }
    
    public void setStakes(JToken stakeJson) {
        //called when an init response is received

        stakes = new List<string>();

        //store all the stake values to a list
        foreach (JToken stake in stakeJson.SelectToken("values")) {
            stakes.Add((string)stake);
        }

        //queues the updateStakes and unlockStartRequestModifiables functions to be invoked on the next update frame on the main thread
        executeInUpdate(() => {
            //passing the current index value to initialise the default position in the stake dropdown
            updateStakes((int)stakeJson.SelectToken("cv"));
            unlockStartRequestModifiables();
        });
    }

    public void onInitRequestSent() {
        //when an init request is sent

        //locks all configuration inputs after an init request is sent
        endpointDropdown.interactable = false;
        gameDropdown.interactable = false;
        playTokenInputField.interactable = false;
    }

    private void setupEndpointDropdown() {
        //double check for the dropdown object
        if(endpointDropdown == null) {
            Debug.Log("Null reference for Endpoint Dropdown");
            return;
        }

        //parse the json file into a jtoken
        JToken parsedEndpointStrings = configurations["locations"];

        List<string> endpointStrings = new List<string>();

        //add each endpoint to a list of strings
        foreach(JToken child in parsedEndpointStrings.Children()) {
            endpointStrings.Add((string)child.SelectToken("endpoint"));

            locationMap.Add((string)child.SelectToken("endpoint"), (string)child.SelectToken("rigging"));
        }

        //add all strings to the list of dropdown options
        endpointDropdown.AddOptions(endpointStrings);
    }

    public string getSelectedEndpoint() {
        if (endpointDropdown == null) {
            Debug.Log("Null reference for Endpoint Dropdown");
            return "";
        }

        //returns the currently selected endpoint
        return endpointDropdown.captionText.text;
    }

    private void setupGameDropdown() {
        games = new Dictionary<string, string>();

        //double check for the dropdown object
        if (gameDropdown == null) {
            Debug.Log("Null reference for Game Dropdown");
            return;
        }


        //parse the json file into a jtoken
        JToken parsedGameObjects = configurations["games"];

        List<string> gameStrings = new List<string>();

        //add each game name to a list of strings and add the map of name to game id to the stored dictionary
        foreach (JToken child in parsedGameObjects.Children()) {
            gameStrings.Add((string)child["name"]);
            games.Add((string)child["name"], (string)child["gameId"]);
        }

        //add all strings to the list of dropdown options
        gameDropdown.AddOptions(gameStrings);
    }

    public string getSelectedGame() {
        if (gameDropdown == null) {
            Debug.Log("Null reference for Game Dropdown");
            return "";
        }

        //returns the game id of the currently selected game
        return games[gameDropdown.captionText.text];
    }

    public string getPlayTokenInput() {
        if (playTokenInputField == null) {
            Debug.Log("Null reference for Play Token Input Field");
            return "";
        }

        //returns the input user play token
        return playTokenInputField.text;
    }

    public string getPlayToken() {
        //returns the selected play token
        return playToken;
    }
    
    public void updateStakes(int initialStakeValueIndex) {
        //double check for the dropdown object
        if (stakeDropDown == null) {
            Debug.Log("Null reference for Stakes Dropdown");
            return;
        }

        //adds all the stakes to the selectable dropdown
        stakeDropDown.AddOptions(stakes);

        //initialise the dropdown to show the current stake index
        stakeDropDown.value = initialStakeValueIndex;
    }

    private void unlockStartRequestModifiables() {
        //unlocks the stake dropdown and profile index input field when in-cycle
        stakeDropDown.interactable = true;
        profileIndexInputField.interactable = true;
    }

    public int getStakeValueIndex() {
        //double check for the dropdown object
        if (stakeDropDown == null) {
            Debug.Log("Null reference for Stakes Dropdown");
            //just because index 3 is usually 1.0 stake
            return 3;
        }

        //returns the index of the seleced stake value in the dropdown
        return stakeDropDown.value;
    }

    public string getProfileIndex() {
        //double check for the dropdown object
        if (profileIndexInputField == null) {
            Debug.Log("Null reference for Profile Index Text Field");
            return "0";
        }

        //return the given profile index or 0 if not set
        return "".Equals(profileIndexInputField.text) ? "0" : profileIndexInputField.text;
    }

    public string getRiggingHTTPLocation() {
        //returns the associate rigging url of the current selected endpoint
        return locationMap[getSelectedEndpoint()];
    }

    public string getInitRequest() {
        //altering a copy of the base init request (TODO: add custom request)
        string request = requestConfigurationSettings.getInitRequest();

        playToken = getPlayTokenInput();
        if ("".Equals(playToken)) {
            //randomly generate a play token
            playToken = ((int)Mathf.Round(UnityEngine.Random.Range(1, MAX_PLAY_TOKEN))).ToString() + "CTRYMT";
        }
        
        //replace the token string in the init request
        request = request.Replace("PLAY_TOKEN", playToken);

        //retrieve the gameid selected
        string gameId = getSelectedGame();

        //make sure a gameid is selected
        if ("".Equals(gameId)) {
            outputManager.queueLogEntry("No game selected", "", LogEntryDetails.EntryType.ERROR, null);
            return null;
        }

        //replace the game string with the selected game
        request = request.Replace("GAME_CODE", gameId);

        return request;
    }

    public string getStartRequest() {
        //altering a copy of the base start request (TODO: add custom request)       
        string request = requestConfigurationSettings.getStartRequest(); ;

        //replace the stake index string in the start request
        request = request.Replace("\"STAKE_INDEX\"", getStakeValueIndex().ToString());

        //replace the profile index string in the start request
        request = request.Replace("\"PROFILE_INDEX\"", getProfileIndex());

        return request;
    }

    public string getUpdate3Request() {
        //returns the base update 3 request (TODO: add custom request)
        string request = requestConfigurationSettings.getUpdate3Request();

        return request;
    }

    public string getUpdate4Request() {
        //returns the base update 4 request (TODO: add custom request)
        string request = requestConfigurationSettings.getUpdate4Request();

        return request;
    }

    public string getGamble3Request() {
        //returns the base gamble 3 request (TODO: add custom request)
        string request = requestConfigurationSettings.getGamble3Request();

        return request;
    }

    public string getGamble4Request() {
        //returns the base gamble 4 request (TODO: add custom request)
        string request = requestConfigurationSettings.getGamble4Request();

        return request;
    }

    public string getEndRequest() {
        //returns the base end request (TODO: add custom request)
        string request = requestConfigurationSettings.getEndRequest();

        return request;
    }
    
    public void toggleRequestConfigActiveState() {
        //toggles the active state of the request configuration panel making it visble/non-visible based on the active in heirarchy boolean
        requestConfigurationSettings.gameObject.SetActive(!requestConfigurationSettings.gameObject.activeInHierarchy);
        requestConfigurationSettings.reawaken();
    }
}
