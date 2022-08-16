using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

public class RequestConfigurationSettings : MonoBehaviour {

    public TMP_InputField requestStringView;
    public Text validationText;
    public OutputManager outputManager;

    private static string baseInitRequest = "{\"type\":\"init\",\"freeplay\":false,\"playToken\":\"PLAY_TOKEN\",\"vendor\":\"bs\",\"game\":{\"code\":\"GAME_CODE\",\"version\":\"0.0.1\"}}";
    private static string baseStartRequest = "{\"type\":\"start\",\"bet\":{\"stake\":{\"cv\":\"STAKE_INDEX\",\"cl\":0}},\"pi\":\"PROFILE_INDEX\"}";
    private static string baseUpdate3Request = "{\"type\":\"update\",\"subType\":3}";
    private static string baseUpdate4Request = "{\"type\":\"update\",\"subType\":4}";
    private static string baseGamble3Request = "{\"type\":\"update\",\"subType\":3,\"opt\":\"double\",\"bonusId\":\"GAMBLE\"}";
    private static string baseGamble4Request = "{\"type\":\"update\",\"subType\":4, \"opt\":\"collect\"}";
    private static string baseEndRequest = "{\"type\":\"end\"}";
    
    private List<Button> buttonList;

    private string customInitRequest = baseInitRequest;
    private string customStartRequest = baseStartRequest;
    private string customUpdate3Request = baseUpdate3Request;
    private string customUpdate4Request = baseUpdate4Request;
    private string customGamble3Request = baseGamble3Request;
    private string customGamble4Request = baseGamble4Request;
    private string customEndRequest = baseEndRequest;

    private int activeButtonIndex;

    private string filePath = Application.streamingAssetsPath + "/Json/RequestConfigurations.json";

    private JObject requestConfigsObject;

    public RequestConfigurationSettings() {
        //this function is called on game load

        //retrieve the request configurations from the json stored in the streaming assets folder
        //streaming assets files can change after game has been built
        try {
            //check for the config file
            if (File.Exists(filePath)) {
                //parse the file into a JObject
                requestConfigsObject = JObject.Parse(File.ReadAllText(filePath));

                //parse the file into the relevant variables
                parseConfigFileObject();
            } else {
                //create the json object with all custom requests
                requestConfigsObject = new JObject(
                    new JProperty("customInitRequest", baseInitRequest),
                    new JProperty("customStartRequest", baseStartRequest),
                    new JProperty("customUpdate3Request", baseUpdate3Request),
                    new JProperty("customUpdate4Request", baseUpdate4Request),
                    new JProperty("customGamble3Request", baseGamble3Request),
                    new JProperty("customGamble4Request", baseGamble4Request),
                    new JProperty("customEndRequest", baseEndRequest)
                );

                // serialize JSON to a string and then write string to a file
                File.WriteAllText(@filePath, StringUtil.getJsonString(requestConfigsObject));
            }

        } catch (JsonReaderException ex) {
            outputManager.queueLogEntry("Error parsing RequestConfigurations.json", ex.Message, LogEntryDetails.EntryType.ERROR, null);
        }
    }

    private void Awake() {
        //this function is only called when the settings panel is first opened
        buttonList = new List<Button>();

        //iterate the children of this object
        for (int i = 0; i < transform.childCount; i++) {
            GameObject child = transform.GetChild(i).gameObject;

            //if the child object has a button component then add to the button list
            if (child.TryGetComponent(out Button button)) {
                buttonList.Add(button);
            }
        }

        //active button is the first in the list (Init)
        setButtons(0);

        //update the input field with the init request
        setRequestView(customInitRequest);
    }

    private void parseConfigFileObject() {
        //parse all the strings into their relevant variables
        customInitRequest = requestConfigsObject.SelectToken("customInitRequest").ToString();
        customStartRequest = requestConfigsObject.SelectToken("customStartRequest").ToString();
        customUpdate3Request = requestConfigsObject.SelectToken("customUpdate3Request").ToString();
        customUpdate4Request = requestConfigsObject.SelectToken("customUpdate4Request").ToString();
        customGamble3Request = requestConfigsObject.SelectToken("customGamble3Request").ToString();
        customGamble4Request = requestConfigsObject.SelectToken("customGamble4Request").ToString();
        customEndRequest = requestConfigsObject.SelectToken("customEndRequest").ToString();
    }

    public void reawaken() {
        //resets the page
        onConfigButtonPressed(activeButtonIndex);
    }

    private void setRequestView(string request) {
        //sets the text in the request view without whitespace
        requestStringView.text = StringUtil.clearWhitespace(request);
    }

    public void onConfigButtonPressed(int identifier) {
        switch(identifier) {
            case 0:
                //init
                setRequestView(customInitRequest);
                break;
            case 1:
                //start
                setRequestView(customStartRequest);
                break;
            case 2:
                //update 3
                setRequestView(customUpdate3Request);
                break;
            case 3:
                //update 4
                setRequestView(customUpdate4Request);
                break;
            case 4:
                //gamble 3
                setRequestView(customGamble3Request);
                break;
            case 5:
                //gamble 4
                setRequestView(customGamble4Request);
                break;
            case 6:
                //end
                setRequestView(customEndRequest);
                break;
        }

        //lock/unlock relevant buttons
        setButtons(identifier);

        //clear the validation text
        resetValidationText();
    }

    public void onUpdateRequestString() {
        
        try {
            JObject.Parse(requestStringView.text);
        } catch (JsonReaderException ex) {
            validationText.color = new Color(1, 0, 0, 1);
            validationText.text = "Invalid json - " + ex.Message;
            return;
        }

        validationText.color = new Color(0, 0.6f, 0.02f, 1);
        validationText.text = "Valid json - Configuration saved";

        switch (activeButtonIndex) {
            case 0:
                //init
                customInitRequest = StringUtil.clearWhitespace(requestStringView.text);
                break;
            case 1:
                //start
                customStartRequest = StringUtil.clearWhitespace(requestStringView.text);
                break;
            case 2:
                //update 3
                customUpdate3Request = StringUtil.clearWhitespace(requestStringView.text);
                break;
            case 3:
                //update 4
                customUpdate4Request = StringUtil.clearWhitespace(requestStringView.text);
                break;
            case 4:
                //gamble 3
                customGamble3Request = StringUtil.clearWhitespace(requestStringView.text);
                break;
            case 5:
                //gamble 4
                customGamble4Request = StringUtil.clearWhitespace(requestStringView.text);
                break;
            case 6:
                //end
                customEndRequest = StringUtil.clearWhitespace(requestStringView.text);
                break;
        }

        //update json file
        updateRequestJsonFile();
    }

    private void updateRequestJsonFile() {
        //safest way is to create full new object and overwrite file

        //create the json object with all custom requests
        requestConfigsObject = new JObject(
            new JProperty("customInitRequest", customInitRequest),
            new JProperty("customStartRequest", customStartRequest),
            new JProperty("customUpdate3Request", customUpdate3Request),
            new JProperty("customUpdate4Request", customUpdate4Request),
            new JProperty("customGamble3Request", customGamble3Request),
            new JProperty("customGamble4Request", customGamble4Request),
            new JProperty("customEndRequest", customEndRequest)
        );

        // serialize JSON to a string and then write string to a file
        File.WriteAllText(@filePath, StringUtil.getJsonString(requestConfigsObject));
    }

    public void onResetRequestString() {

        validationText.color = new Color(0, 0, 0, 1);
        validationText.text = "Request reset - Configuration saved";

        switch (activeButtonIndex) {
            case 0:
                //init
                customInitRequest = baseInitRequest;
                setRequestView(customInitRequest);
                break;
            case 1:
                //start
                customStartRequest = baseStartRequest;
                setRequestView(customStartRequest);
                break;
            case 2:
                //update 3
                customUpdate3Request = baseUpdate3Request;
                setRequestView(customUpdate3Request);
                break;
            case 3:
                //update 4
                customUpdate4Request = baseUpdate4Request;
                setRequestView(customUpdate4Request);
                break;
            case 4:
                //gamble 3
                customGamble3Request = baseGamble3Request;
                setRequestView(customGamble3Request);
                break;
            case 5:
                //gamble 4
                customGamble4Request = baseGamble4Request;
                setRequestView(customGamble4Request);
                break;
            case 6:
                //end
                customEndRequest = baseEndRequest;
                setRequestView(customEndRequest);
                break;
        }

        //update json file
        updateRequestJsonFile();
    }

    public void resetValidationText() {
        //clears the validation text
        validationText.text = "";
    }

    private void setButtons(int index) {
        //iterate the button list, locking the new active button and unlocking the rest
        for(int i = 0; i < buttonList.Count; i++) {
            buttonList[i].interactable = i != index ? true : false;
        }

        //updating the global stored active index
        activeButtonIndex = index;
    }

    public string getInitRequest() {
        //returns the custom init request
        return StringUtil.clearWhitespace(customInitRequest);
    }

    public string getStartRequest() {
        //returns the custom start request
        return StringUtil.clearWhitespace(customStartRequest);
    }

    public string getUpdate3Request() {
        //returns the custom update 3 request
        return StringUtil.clearWhitespace(customUpdate3Request);
    }

    public string getUpdate4Request() {
        //returns the custom update 4 request
        return StringUtil.clearWhitespace(customUpdate4Request);
    }

    public string getGamble3Request() {
        //returns the custom gamble 3 request
        return StringUtil.clearWhitespace(customGamble3Request);
    }

    public string getGamble4Request() {
        //returns the custom gamble 4 request
        return StringUtil.clearWhitespace(customGamble4Request);
    }

    public string getEndRequest() {
        //returns the custom end request
        return StringUtil.clearWhitespace(customEndRequest);
    }
}