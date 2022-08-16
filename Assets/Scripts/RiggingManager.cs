using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class RiggingManager : MonoBehaviour {

    public InputField riggingLocationInputField;
    public TMP_InputField riggingRequestInputField;
    public Text riggingStatusCodeText;

    public ConfigurationManager configurationManager;
    public OutputManager outputManager;

    public int maxStoredRequests;

    private List<string> sentRiggingRequests = new List<string>();
    private string storedCurrentRequest = null;
    private int requestIndex = -1;

    private string playToken = "";

    public void onInitResponse(string playToken) {
        //Store the new active play token
        this.playToken = playToken;

        //sets the location address in the input file along with the play token
        riggingLocationInputField.text = configurationManager.getRiggingHTTPLocation() + this.playToken;
    }

    public void sendRiggingRequest() {
        string riggingLocationAddress = getRiggingLocationAddress();

        //check for empty location address before setting up the web request
        if ("".Equals(riggingLocationAddress)) {
            outputManager.queueLogEntry("Malformed rigging location address", "No address given", LogEntryDetails.EntryType.ERROR, null);
            return;
        }

        //retrieve the rigging request string 
        string riggingRequest = getRiggingRequest();

        //removes all white space characters (' ' and '\t' and '\n')
        riggingLocationAddress = StringUtil.clearWhitespace(riggingLocationAddress);
        riggingRequest = StringUtil.clearWhitespace(riggingRequest);
        
        //set up a post web request (get request hasn't been need in the past so far)
        UnityWebRequest uwr = new UnityWebRequest(riggingLocationAddress, "POST");
        //convert the rigging request to a byte array ready to be sent
        byte[] txtToSend = new System.Text.UTF8Encoding().GetBytes(riggingRequest);
        //try setting up the web request
        try {
            uwr.uploadHandler = new UploadHandlerRaw(txtToSend);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "text/plain");
        } catch (Exception ex) {
            //a problem with the request data, can't be sent
            outputManager.queueLogEntry("Malformed rigging location address", ex.Message, LogEntryDetails.EntryType.ERROR, null);
            return;
        }

        //storing request so can be used again
        storeRequest(riggingRequest);

        //update log
        outputManager.queueLogEntry("Sent rigging request", riggingRequest, LogEntryDetails.EntryType.RIGGING, null);

        //start coroutine to send the web request
        StartCoroutine(postRequest(uwr));
    }

    IEnumerator postRequest(UnityWebRequest uwr) {

        //Send the request then wait here until it returns
        yield return uwr.SendWebRequest();

        //riggingStatusCodeText.text = DateTime.Now.ToString("HH:mm:ss") + " Status Code - " + uwr.responseCode;
        riggingStatusCodeText.text = "Status Code - " + uwr.responseCode;
    }

    private void storeRequest(string request) {
        //add request to end the stored list
        sentRiggingRequests.Add(request);

        //check if list size
        if (sentRiggingRequests.Count > maxStoredRequests) {
            //remove requests from the front of the list
            for(int i = 0; i < (sentRiggingRequests.Count - maxStoredRequests); i++) {
                sentRiggingRequests.RemoveAt(0);
            }
        }

        //set the current request index to the end of the list
        requestIndex = sentRiggingRequests.Count - 1;
    }

    public string getRiggingLocationAddress() {
        return riggingLocationInputField.text;
    }

    public string getRiggingRequest() {
        return riggingRequestInputField.text;
    }

    public void getPreviousRequest() {
        //no requests to iterate through
        if(sentRiggingRequests.Count == 0) {
            return;
        }

        //index at end of list, current stored request is null and the input field text doesn't match last string in the list
        if(requestIndex == sentRiggingRequests.Count - 1 && storedCurrentRequest == null && !getRiggingRequest().Equals(sentRiggingRequests[sentRiggingRequests.Count - 1])) {
            //get the current text from the input field, removing any whitespace, and store so that can come back to the string
            storedCurrentRequest = StringUtil.clearWhitespace(getRiggingRequest());
        } else if(requestIndex > 0) {
            //go to previous request index
            requestIndex--;
        }

        //load the request text into the input field
        riggingRequestInputField.text = sentRiggingRequests[requestIndex];
    }

    public void getNextRequest() {
        //no requests to iterate through
        if (sentRiggingRequests.Count == 0) {
            return;
        }

        if (requestIndex == sentRiggingRequests.Count - 1  && storedCurrentRequest != null) {
            //load the current stored text the user was working on
            riggingRequestInputField.text = storedCurrentRequest;
            //get rid of the stored text
            storedCurrentRequest = null;
        } else if (requestIndex < sentRiggingRequests.Count - 1) {
            //go to next request index
            requestIndex++;
            //load the request text into the input field
            riggingRequestInputField.text = sentRiggingRequests[requestIndex];
        }

    }
}
