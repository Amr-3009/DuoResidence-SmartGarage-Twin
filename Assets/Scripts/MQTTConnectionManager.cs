using System;
using System.Text;
using System.Collections.Concurrent; // Required for Thread-Safe Queue buffers
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

public class MQTTConnectionManager : MonoBehaviour
{
    // Master Access Singleton
    public static MQTTConnectionManager Instance { get; private set; }

    [Header("Broker Configuration")]
    [Tooltip("Set your target host domain URL string here (e.g., broker.hivemq.com or your cloud address).")]
    [SerializeField] private string brokerAddress = "broker.hivemq.com";
    [SerializeField] private int brokerPort = 1883;
    [SerializeField] private string clientId = "DuoResidence_Twin_Client";

    private MqttClient mqttClient;

    // Simple struct container to safely transport background network strings across thread boundaries
    private struct MqttMessageData
    {
        public string topic;
        public string payload;
    }

    // High-performance, lock-free concurrent queue buffer
    private ConcurrentQueue<MqttMessageData> incomingMessageQueue = new ConcurrentQueue<MqttMessageData>();

    // Global static event wrapper that external synchronization scripts listen to
    public static Action<string, string> OnTelemetryMessageReceived;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeMQTT();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeMQTT()
    {
        try
        {
            mqttClient = new MqttClient(brokerAddress, brokerPort, false, null, null, MqttSslProtocols.None);
            mqttClient.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
            mqttClient.Connect(clientId);
            Debug.Log($"<color=#00FF00><b>[MQTT Connection]:</b></color> Handshake successful! Connected safely to broker at {brokerAddress}:{brokerPort}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MQTT Connection Failed Init Critical]: {ex.Message}");
        }
    }

    /// <summary>
    /// EXECUTING ON BACKGROUND THREAD: Safely captures the packet streams and buffers them into the concurrent queue.
    /// </summary>
    private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string topic = e.Topic;
        string payload = Encoding.UTF8.GetString(e.Message);

        // Safely push data into our queue instead of invoking Unity material changes on this background thread
        incomingMessageQueue.Enqueue(new MqttMessageData { topic = topic, payload = payload });
    }

    /// <summary>
    /// EXECUTING ON UNITY MAIN THREAD: Safely flushes out the message buffer queue context during frame updates.
    /// </summary>
    private void Update()
    {
        // Drain the entire thread-safe queue every frame on Unity's main processing loop thread
        while (incomingMessageQueue.TryDequeue(out MqttMessageData msgData))
        {
            // This safely invokes your event hooks, executing material swaps on the main thread!
            OnTelemetryMessageReceived?.Invoke(msgData.topic, msgData.payload);
        }
    }

    /// <summary>
    /// Encodes and publishes outbound data packets.
    /// </summary>
    public void PublishTopic(string topic, string payload, bool retain = false)
    {
        if (mqttClient != null && mqttClient.IsConnected)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(payload);
            mqttClient.Publish(topic, messageBytes, MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, retain);
        }
        else
        {
            Debug.LogWarning("[MQTT Publish Warning]: Client is currently offline. Cannot dispatch network packet.");
        }
    }

    /// <summary>
    /// Instructs the underlying network client instance to subscribe to a target channel wildcard tree.
    /// </summary>
    public void SubscribeToTopic(string topic)
    {
        if (mqttClient != null && mqttClient.IsConnected)
        {
            mqttClient.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
            Debug.Log($"<color=#00FFFF><b>[MQTT Subscription]:</b></color> Registered tracking channel route: {topic}");
        }
        else
        {
            Debug.LogWarning($"[MQTT Subscription Failure]: Cannot track channel route {topic}. Network client is detached.");
        }
    }

    private void OnDestroy()
    {
        if (mqttClient != null && mqttClient.IsConnected)
        {
            mqttClient.MqttMsgPublishReceived -= Client_MqttMsgPublishReceived;
            mqttClient.Disconnect();
        }
    }
}