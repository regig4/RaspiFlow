#!/usr/bin/env python
import time
import os
import json
#import serial
from azure.eventhub import EventHubProducerClient, EventData
from azure.servicebus import ServiceBusClient, ServiceBusMessage
from random import randrange 

producer = EventHubProducerClient.from_connection_string(
    conn_str=os.environ.get("ConnectionStrings__event-hubs"),
    consumer_group="$Default",
    eventhub_name="eh1")

servicebus_client = ServiceBusClient.from_connection_string(
    conn_str=os.environ.get("ConnectionStrings__myservicebus"),
    logging_enable=True
)

prev_simulated_sensor_read = 20

simulated_reads = True

device0 = "/sys/bus/iio/devices/iio:device0"

def read_simulation(reader, sender, prev_simulated_sensor_read):
    while True:
        time.sleep(11)  # azure event hub emulator allows new req after 10 sec
        event_data_batch = producer.create_batch()
        event_data = EventData("Sensor read")
        simulated_sensor_read = randrange(prev_simulated_sensor_read - 5, prev_simulated_sensor_read + 5)
        prev_simulated_sensor_read = simulated_sensor_read 
        event_data.properties = {"simulated_sensor_read": simulated_sensor_read}
        event_data_batch.add(event_data)
        producer.send_batch(event_data_batch)

        message = ServiceBusMessage(json.dumps({ "data": simulated_sensor_read, "timestamp": int(time.time() * 1000)   }))
        sender.send_messages(message)


# def read_sds001(reader, sender):
#     ser = serial.Serial("/dev/ttyUSB0", baudrate=9600, timeout=2)

#     while True:
#         data = ser.read(10)
#         if data[0] == 0xAA and data[1] == 0xC0 and data[9] == 0xAB:
#             pm25 = (data[3] << 8) + data[2]
#             pm10 = (data[5] << 8) + data[4]
#             print(f"PM2.5: {pm25 / 10.0} µg/m³, PM10: {pm10 / 10.0} µg/m³")
#             time.sleep(11)  # azure event hub emulator allows new req after 10 sec
#             event_data_batch = producer.create_batch()
#             event_data = EventData("Sensor read")
#             sensor_read = m25 / 10.0
#             event_data.properties = {"sensor_read": sensor_read}
#             event_data_batch.add(event_data)
#             producer.send_batch(event_data_batch)

#             message = ServiceBusMessage(json.dumps({ "data": sensor_read }))
#             sender.send_messages(message)



# def readFirstLine(filename):
#     try:
#         f = open(filename,"rt")
#         value =  int(f.readline())
#         f.close()
#         return True, value
#     except ValueError:
#         f.close()
#         return False,-1
#     except OSError:
#         return False,0
# 
# 
# try:
#     while True:
#         Flag, Temperature = readFirstLine(device0+"/in_temp_input")
#         print("Temperature:",end="")
#         if Flag:
#             print(Temperature // 1000,"\u2103",end="\t")
#         else:
#             print("N.A.",end="\t")
# 
#         Flag, Humidity = readFirstLine(device0+"/in_humidityrelative_input")
#         print("Humidity:",end="")
#         if Flag:
#             print(Humidity // 1000,"%")
#         else:
#             print("N.A.")
#         time.sleep(2.0)
# except KeyboardInterrupt:
#     pass

with producer:
    with servicebus_client:
        sender = servicebus_client.get_queue_sender(queue_name="myqueue")
        with sender:
            if simulated_reads:
                read_simulation(producer, sender, prev_simulated_sensor_read)
#            else: 
#                read_sds001(producer, sender)
