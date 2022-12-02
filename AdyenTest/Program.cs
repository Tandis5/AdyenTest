using Adyen;
using Adyen.HttpClient;
using Adyen.Model.Nexo.Message;
using Adyen.Model.Nexo;
using Adyen.Security;
using Adyen.Service;
using Adyen.Model.Terminal;

const string ENDPOINT = "https://1.1.1.1:8443/nexo/";
const string KEY_IDENTIFIER = "key identifier";
const string PASSWORD = "password";
const string TERMINAL_ID = "terminal id";

EncryptionCredentialDetails encryptionCredentialDetails = new EncryptionCredentialDetails
{
    AdyenCryptoVersion = 1,
    KeyIdentifier = KEY_IDENTIFIER,
    Password = PASSWORD,
    KeyVersion = 1
};

Config paymentConfig = new Config
{
    Endpoint = ENDPOINT,
    Environment = Adyen.Model.Enum.Environment.Test,
    HttpRequestTimeout = 30000
};

Client client = new Client(paymentConfig);
PosPaymentLocalApi paymentClient = new PosPaymentLocalApi(client);

paymentClient.Client.LogCallback += message =>
{
    Console.WriteLine($"{DateTime.Now:MM/dd/yy - HH:mm:ss.fff} - {message}");
};

SaleToPOIResponse saleToPOIResponse = SendCardAcquisitionRequest();
LogMessage(step: "Card Acquisition",
    result: ((CardAcquisitionResponse)saleToPOIResponse.MessagePayload).Response.Result,
    errorCondition: ((CardAcquisitionResponse)saleToPOIResponse.MessagePayload).Response.ErrorCondition);

string transactionId = ((CardAcquisitionResponse)saleToPOIResponse.MessagePayload).POIData.POITransactionID.TransactionID;
saleToPOIResponse = SendPaymentRequest(transactionId);
LogMessage(step: "Payment",
    result: ((PaymentResponse)saleToPOIResponse.MessagePayload).Response.Result,
    errorCondition: ((PaymentResponse)saleToPOIResponse.MessagePayload).Response.ErrorCondition);

await Task.Delay(2000);

try
{
    saleToPOIResponse = SendIdleScreen();
    LogMessage(step: "Idle Screen",
        result: ((DisplayResponse)saleToPOIResponse.MessagePayload).OutputResult[0].Response.Result,
        errorCondition: ((DisplayResponse)saleToPOIResponse.MessagePayload).OutputResult[0].Response.ErrorCondition);
}
catch (HttpClientException e)
{
    Console.WriteLine($"{DateTime.Now:MM/dd/yy - HH:mm:ss.fff} - Idle Screen error - {e}");
}

await Task.Delay(2000);

try
{
    saleToPOIResponse = SendIdleScreen();
    LogMessage(step: "Idle Screen",
        result: ((DisplayResponse)saleToPOIResponse.MessagePayload).OutputResult[0].Response.Result,
        errorCondition: ((DisplayResponse)saleToPOIResponse.MessagePayload).OutputResult[0].Response.ErrorCondition);
}
catch (HttpClientException e)
{
    Console.WriteLine($"{DateTime.Now:MM/dd/yy - HH:mm:ss.fff} - Idle Screen error - {e}");
}

void LogMessage(string step, ResultType result, ErrorConditionType? errorCondition)
{
    Console.WriteLine($"{DateTime.Now:MM/dd/yy - HH:mm:ss.fff} - Step: [{step}] - Result: [{result.ToString()}] - Error Condition: [{(errorCondition == null ? "N/A" : errorCondition.ToString())}]");
}

SaleToPOIResponse SendSurvey()
{
    SaleToPOIRequest pinpadRequest = new SaleToPOIRequest();

    pinpadRequest.MessageHeader = new MessageHeader
    {
        MessageCategory = MessageCategoryType.Input,
        MessageClass = MessageClassType.Device,
        MessageType = MessageType.Request,
        POIID = TERMINAL_ID,
        SaleID = Environment.MachineName,
        ServiceID = GetUniqueId()
    };

    InputRequest inputRequest = new InputRequest();

    DisplayOutput displayOutput = inputRequest.DisplayOutput = new DisplayOutput();
    displayOutput.Device = DeviceType.CustomerDisplay;
    displayOutput.InfoQualify = InfoQualifyType.Display;

    OutputContent outputContent = inputRequest.DisplayOutput.OutputContent = new OutputContent();
    outputContent.OutputFormat = OutputFormatType.Text;
    outputContent.PredefinedContent = new PredefinedContent()
    {
        ReferenceID = "MenuButtons"
    };
    outputContent.OutputText = new OutputText[]
    {
        new OutputText() { Text = "How long did you wait in line at checkout?" }
    };

    displayOutput.MenuEntry = new MenuEntry[]
    {
        new MenuEntry()
        {
            OutputFormat = OutputFormatType.Text,
            OutputText = new OutputText[] { new OutputText() { Text = "I waited too long" } }
        },
        new MenuEntry()
        {
            OutputFormat = OutputFormatType.Text,
            OutputText = new OutputText[] { new OutputText() { Text = "I had a short wait" } }
        },
        new MenuEntry()
        {
            OutputFormat = OutputFormatType.Text,
            OutputText = new OutputText[] { new OutputText() { Text = "I did not wait" } }
        }
    };

    inputRequest.InputData = new InputData()
    {
        Device = DeviceType.CustomerInput,
        InfoQualify = InfoQualifyType.Input,
        InputCommand = InputCommandType.GetMenuEntry,
        MaxInputTime = 1
    };

    pinpadRequest.MessagePayload = inputRequest;

    try
    {
        paymentClient.Client.Config.HttpRequestTimeout = 10000;
        return paymentClient.TerminalApiLocal(pinpadRequest, encryptionCredentialDetails);
    }
    finally
    {
        paymentClient.Client.Config.HttpRequestTimeout = 140000;
    }
}

SaleToPOIResponse SendIdleScreen()
{
    SaleToPOIRequest idleRequest = new SaleToPOIRequest
    {
        MessageHeader = new MessageHeader
        {
            MessageCategory = MessageCategoryType.Display,
            MessageClass = MessageClassType.Device,
            MessageType = MessageType.Request,
            POIID = TERMINAL_ID,
            ServiceID = GetUniqueId(),
            SaleID = Environment.MachineName
        },
        MessagePayload = new DisplayRequest
        {
            DisplayOutput = new[]
            {
                new DisplayOutput
                {
                    Device = DeviceType.CustomerDisplay,
                    InfoQualify = InfoQualifyType.Display,
                    OutputContent = new OutputContent
                    {
                        OutputFormat = OutputFormatType.MessageRef,
                        PredefinedContent = new PredefinedContent
                        {
                            ReferenceID = "Idle"
                        }
                    }
                }
            }
        }
    };

    try
    {
        paymentClient.Client.Config.HttpRequestTimeout = 10000;
        return paymentClient.TerminalApiLocal(idleRequest, encryptionCredentialDetails);
    }
    finally
    {
        paymentClient.Client.Config.HttpRequestTimeout = 140000;
    }
}

SaleToPOIResponse SendCardAcquisitionRequest()
{
    SaleToPOIRequest saleToPoiRequest = new SaleToPOIRequest
    {
        MessageHeader = new MessageHeader
        {
            MessageType = MessageType.Request,
            MessageClass = MessageClassType.Service,
            MessageCategory = MessageCategoryType.CardAcquisition,
            POIID = TERMINAL_ID,
            SaleID = Environment.MachineName,
            ServiceID = GetUniqueId()
        },
        MessagePayload = new CardAcquisitionRequest
        {
            SaleData = new SaleData
            {
                SaleTransactionID = new TransactionIdentification
                {
                    TransactionID = "1234",
                    TimeStamp = DateTime.Now
                },
                TokenRequestedType = TokenRequestedType.Customer
            },
            CardAcquisitionTransaction = new CardAcquisitionTransaction
            {
                TotalAmount = 10.00M
            }
        }
    };

    return paymentClient.TerminalApiLocal(saleToPoiRequest, encryptionCredentialDetails);
}

SaleToPOIResponse SendPaymentRequest(string transactionId)
{
    SaleToPOIRequest saleToPoiRequest = new SaleToPOIRequest
    {
        MessageHeader = new MessageHeader
        {
            MessageType = MessageType.Request,
            MessageClass = MessageClassType.Service,
            MessageCategory = MessageCategoryType.Payment,
            POIID = TERMINAL_ID,
            SaleID = Environment.MachineName,
            ServiceID = GetUniqueId()
        },
        MessagePayload = new PaymentRequest
        {
            SaleData = new SaleData
            {
                SaleTransactionID = new TransactionIdentification
                {
                    TransactionID = "1234",
                    TimeStamp = DateTime.Now
                },
                SaleToAcquirerData = new SaleToAcquirerData
                {
                    TenderOption = "AllowPartialAuthorisation",
                    AdditionalData = null
                },
                TokenRequestedType = TokenRequestedType.Customer,
            },
            PaymentTransaction = new PaymentTransaction
            {
                AmountsReq = new AmountsReq
                {
                    Currency = "USD",
                    RequestedAmount = 10.00M
                }
            },
            PaymentData = new PaymentData
            {
                CardAcquisitionReference = new TransactionIdentification
                {
                    TimeStamp = DateTime.Now,
                    TransactionID = transactionId
                }
            }
        }
    };

    return paymentClient.TerminalApiLocal(saleToPoiRequest, encryptionCredentialDetails);
}

string GetUniqueId()
{
    return long.Parse(DateTime.Now.Ticks.ToString().Substring(2, 12)).ToString("X2");
}
