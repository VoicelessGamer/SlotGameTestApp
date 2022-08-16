using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System;

public class OutputManager : MonoBehaviour {

    public EventManager eventManager;

    public GameObject logScrollView;
    public TMP_InputField featureInputField;
    public TMP_InputField awardInputField;
    public TMP_InputField responseJsonInputField;
    public Text websocketInformationText;

    public GameObject clickableLogEntry;
    public GameObject nonClickableLogEntry;
    public int maxLogEntries = -1;
    
    private JObject responseObject;
    private bool responseJsonLoaded = false;

    private List<LogEntryDetails> queuedLogEntries;
    List<LogEntryDetails> copiedLogEntries = new List<LogEntryDetails>();
    private Queue<GameObject> logEntryObjects;

    private GameObject logContent;
    private ScrollRect logScrollViewScrollRect;

    private void Awake() {
        queuedLogEntries = new List<LogEntryDetails>();
        logEntryObjects = new Queue<GameObject>();

        logContent = logScrollView.transform.Find("Viewport").Find("LogContent").gameObject;
        logScrollViewScrollRect = logScrollView.GetComponent<ScrollRect>();
    }

    public void onResponse(JObject jObject) {
        //called on a server response

        //store the full response object
        responseObject = jObject;

        //removed this from every update to increase performance. Json can now be loaded from a button click
        //updateResponseJsonView();
        responseJsonLoaded = false;
        responseJsonInputField.text = "";
        updateFeatureView();
        updateAwardView();
    }

    public void queueNextWebsocketMessage(string message) {
        websocketInformationText.text = message;
    }

    public void queueLogEntry(string title, string msg, LogEntryDetails.EntryType type, string gameId) {
        //set up a LogEntryDetails object with the given parameters
        LogEntryDetails details = new LogEntryDetails(title, msg, type);

        //set a game id to the object if given. This will be used by the other classes to identify configurations that need to be used
        if (gameId != null) {
            details.setGameId(gameId);
        }

        //add the details to the list of log entries to be updated
        queuedLogEntries.Add(details);

        //queues the updateLogEntries coroutine to be invoked on the next update frame on the main thread
        StartCoroutine(updateLogEntries());

    }

    public void onOpenWebsocketConnection() {
        //called when opening a new websocket connection
        websocketInformationText.text = "Awaiting connection to server...";

        //clear the text in all 'view' input fields
        featureInputField.text = "";
        awardInputField.text = "";
        responseJsonInputField.text = "";
    }

    private void updateFeatureView() {
        //nowhere to display content
        if (featureInputField.text == null) {
            Debug.Log("Null reference for feature content");
            return;
        }
        
        //collect the persistent and consumable features from the server response
        JToken persistentFeaturesArray = responseObject.GetValue("game").SelectToken("view.pf");
        JToken consumableFeaturesArray = responseObject.GetValue("game").SelectToken("view.cf");
        
        string displayedString = "PERSISTENT FEATURES:\n";

        //parse the persistent features into the display string
        if(persistentFeaturesArray.HasValues) {
            //iterate each individual feature
            foreach (JToken featureObject in persistentFeaturesArray) {
                //parse the current persistent feature
                string parsedFeature = StringUtil.getJsonString(JObject.Parse(featureObject.ToString()));

                //add a separator between each persistent feature
                displayedString += parsedFeature;
                if (featureObject.Next != null) {
                    displayedString += "\n--------------------------------------\n";
                }
            }
        }

        //add a separator between persistent features and consumable features
        displayedString += "\n****************************************************************************\n";
        displayedString += "CONSUMABLE FEATURES:\n";

        //consumableFeaturesArray will be null on init response
        if (consumableFeaturesArray != null && consumableFeaturesArray.HasValues) {
            //iterate each individual feature
            foreach (JToken featureObject in consumableFeaturesArray) {
                //parse the current consumable feature
                string parsedFeature = StringUtil.getJsonString(JObject.Parse(featureObject.ToString()));

                //add a separator between each consumable feature
                displayedString += parsedFeature;
                if (featureObject.Next != null) {
                    displayedString += "\n--------------------------------------\n";
                }
            }
        }

        //add the display string to the content text
        featureInputField.text = displayedString;
    }

    private void updateAwardView() {
        //nowhere to display content
        if (awardInputField.text == null) {
            Debug.Log("Null reference for award content");
            return;
        }

        JToken betsArray = responseObject.GetValue("game").SelectToken("view.bets");
        JToken winsArray;

        if (betsArray.HasValues) {
            //currently only parsing the first bet object
            //TODO: Add looping for individual bets with some form of separation (dutch game update)
            JToken bets = responseObject.GetValue("game").SelectToken("view.bets");
            winsArray = bets.Last.SelectToken("wins");
        } else {
            //no wins to display
            return;
        }

        string displayedString = "";

        //iterate each individual win
        foreach(JToken winObject in winsArray) {
            string parsedAward = "";

            //parse the award based on its type
            switch((string)winObject.SelectToken("type")) {
                case "REEL_WIN":
                    parsedAward = parseReelWin(winObject);
                    break;
                case "ANYWAYS_WIN":
                    parsedAward = parseAnywaysWin(winObject);
                    break;
                case "PRIZE_WIN":
                    parsedAward = parsePrizeWin(winObject);
                    break;
                case "OVERLAY_WIN":
                    parsedAward = parseOverlayWin(winObject);
                    break;
                default:                    
                    parsedAward = parseDefaultAward(winObject);
                    break;
            }

            //add separator between each award
            displayedString += parsedAward + "---------------\n";
        }

        //add the display string to the content text
        awardInputField.text = displayedString;
    }

    private string parseDefaultAward(JToken winObject) {
        string parsedString = "";

        //all awards have a type and cash amount
        parsedString += "Type: " + (string)winObject.SelectToken("type") + "\n";
        parsedString += "Cash: " + (string)winObject.SelectToken("cash") + "\n";

        return parsedString;
    }

    private string parseReelWin(JToken winObject) {
        string parsedString = "";

        //parse the base variables and variables found in a reel win
        parsedString += parseDefaultAward(winObject);
        parsedString += "Coin: " + (string)winObject.SelectToken("coin") + "\n";
        parsedString += "Points: " + parsePoints(winObject.SelectToken("pos")) + "\n";
        parsedString += "Winline Index: " + (string)winObject.SelectToken("wi") + "\n";
        parsedString += "Winline Multiplier: " + (string)winObject.SelectToken("wm") + "\n";

        return parsedString;
    }

    private string parseAnywaysWin(JToken winObject) {
        string parsedString = "";

        //parse the base variables and variables found in an anyways win
        parsedString += parseDefaultAward(winObject);
        parsedString += "Coin: " + (string)winObject.SelectToken("coin") + "\n";
        parsedString += "Points: " + parsePoints(winObject.SelectToken("pos")) + "\n";
        parsedString += "Winline Multiplier: " + (string)winObject.SelectToken("wm") + "\n";

        parsedString += "Win Count: ";
        //parse the win count
        if (winObject.SelectToken("wc").Type.Equals(JTokenType.Object)) {
            //parse the case that win count is an object containing the award id and amount of that award won
            foreach (JProperty property in winObject.SelectToken("wc")) {
                parsedString += "[" + property.Name + ": " + property.Value + "], ";
            }
            parsedString += "\n";
        } else {
            //parse the case that win count is sent through as a grand total amount of awards won
            parsedString += (string)winObject.SelectToken("wc") + "\n";
        }

        return parsedString;
    }

    private string parsePrizeWin(JToken winObject) {
        string parsedString = "";

        //parse the base variables and variables found in a prize win
        parsedString += parseDefaultAward(winObject);
        parsedString += "Feature Id: " + (string)winObject.SelectToken("fi") + "\n";

        return parsedString;
    }

    private string parseOverlayWin(JToken winObject) {
        string parsedString = "";

        //parse the base variables and variables found in a prize win and an overlay win
        parsedString += parsePrizeWin(winObject);
        parsedString += "Points: " + parsePoints(winObject.SelectToken("pos")) + "\n";

        return parsedString;
    }

    private string parsePoints(JToken pointsArray) {
        string parsedString = "";

        //parsing points in the preferred layout
        foreach(JToken position in pointsArray) {
            parsedString += "[" + (string)position.SelectToken("ri") + ", " + (string)position.SelectToken("si") + "]";
            //only adding comma separation if not on the last object
            if(position != pointsArray.Last) {
                parsedString += ", ";
            }
        }

        return parsedString;
    }

    public void onLoadResponseJsonView() {
        //quick return if response json is already loaded or there is nothing to load
        if(responseJsonLoaded || responseObject == null) {
            return;
        }

        //setting the response json text to a prettified version of the response string to make it easier to read
        responseJsonInputField.text = StringUtil.getJsonString(responseObject);

        responseJsonLoaded = true;
    }

    IEnumerator updateLogEntries() {

        //Clear the old log entries from the copiedLogEntries queue
        copiedLogEntries.Clear();

        //lock the queuedLogEntries list to prevent other threads accessing
        lock (queuedLogEntries) {
            //Copy queuedLogEntries to the copiedLogEntries variable
            copiedLogEntries.AddRange(queuedLogEntries);
            //Clear the queuedLogEntries after copying
            queuedLogEntries.Clear();
        }

        //adding all the queued string to the log
        foreach (LogEntryDetails entry in copiedLogEntries) {
            GameObject logEntry = null;
            Text txt = null;

            if (entry.getEntryType() == LogEntryDetails.EntryType.ENDPOINT_RESPONSE) {
                //server responses clickable to allow reloading of response details
                logEntry = createClickableLogEntry(new Vector3(0, 0, 0));
                RectTransform btn = (RectTransform)logEntry.transform.Find("Button");
                txt = btn.gameObject.GetComponentInChildren<Text>();

                //update text and text colour
                txt.color = entry.getEntryColour();
                txt.text = entry.getFullDisplayString(false);

                RectTransform entryTransform = (RectTransform)logEntry.transform;

                //if text extends past the default width then update the width to update content scroller
                if(txt.preferredWidth > entryTransform.sizeDelta.x) {
                    btn.sizeDelta = new Vector2(txt.preferredWidth, btn.sizeDelta.y);

                    //entry width controls content scrolling so width has to be updated along with button
                    entryTransform.sizeDelta = new Vector2(txt.preferredWidth, entryTransform.sizeDelta.y);
                }

                //add an onClick event listener to button to call the reloadResponse function in request system, passing in the entry message
                btn.gameObject.GetComponent<Button>().onClick.AddListener(() => eventManager.reloadResponse(entry.getMessage(), entry.getGameId()));
            } else {
                logEntry = createNonClickableLogEntry(new Vector3(0, 0, 0));
                txt = logEntry.GetComponentInChildren<Text>();

                //update text and text colour
                txt.color = entry.getEntryColour();
                txt.text = entry.getFullDisplayString();

                //if text extends past the default width then update the width to update content scroller
                RectTransform entryTransform = (RectTransform)logEntry.transform;
                if (txt.preferredWidth > entryTransform.sizeDelta.x) {
                    RectTransform textTransform = (RectTransform)txt.transform;
                    textTransform.sizeDelta = new Vector2(txt.preferredWidth, textTransform.sizeDelta.y);

                    //entry width controls content scrolling so width has to be updated along with text
                    entryTransform.sizeDelta = new Vector2(txt.preferredWidth, entryTransform.sizeDelta.y);
                }
            }
            
            logEntryObjects.Enqueue(logEntry);
        }

        //make sure to remove entries from the top once the max log entries have been reached
        if (maxLogEntries != -1 && logEntryObjects.Count > maxLogEntries) {
            //only delete the number of entries that would drop the current total to the max
            for(int i = 0; i < logEntryObjects.Count - maxLogEntries; i++) {
                Destroy(logEntryObjects.Dequeue());
            }
        }

        //have to wait for end of frame update to move scroll bar to bottom
        yield return new WaitForEndOfFrame();
        logScrollViewScrollRect.verticalNormalizedPosition = 0;
    }

    public void onClearLog() {
        //deletes all log entries
        for (int i = logEntryObjects.Count; i > 0; i--) {
            Destroy(logEntryObjects.Dequeue());
        }
    }

    private GameObject createClickableLogEntry(Vector3 position) {
        //instantiate creates a new instance of the provided game object
        GameObject entry = Instantiate(clickableLogEntry, position, Quaternion.identity);
        //sets the parent of the created entry to this object. Second parameter, if true,
        //the parent-relative position, scale and rotation are modified such that the object keeps the same world space position, rotation and scale as before.
        entry.transform.SetParent(logContent.transform, false);
        return entry;
    }

    private GameObject createNonClickableLogEntry(Vector3 position) {
        //instantiate creates a new instance of the provided game object
        GameObject entry = Instantiate(nonClickableLogEntry, position, Quaternion.identity);
        //sets the parent of the created entry to this object. Second parameter, if true,
        //the parent-relative position, scale and rotation are modified such that the object keeps the same world space position, rotation and scale as before.
        entry.transform.SetParent(logContent.transform, false);
        return entry;
    }
}
