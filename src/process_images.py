from urllib.request import urlopen
import pickle
import urllib
import json
from http.cookiejar import CookieJar
import os
from collections import defaultdict
import traceback
from subprocess import Popen, PIPE
import uuid
import threading
import time
from datetime import datetime
import subprocess
from azurestorageprovider import AzureStorageProvider
import shutil
from astral import Astral
import pytz
from pytz import timezone
from PIL import Image
from PIL import ImageDraw
from PIL import ImageFont
import re

class ProcessImages(object):

    def __init__(self, config, camera_buffer, video_path, motion_path, concat_file_name, camera_path, ffmpeg_path, camera, file_id):

        self.config = config
        self.camera_buffer = camera_buffer
        self.video_path = video_path
        self.motion_path = motion_path
        self.concat_file_name = concat_file_name
        self.camera_path = camera_path
        self.ffmpeg_path = ffmpeg_path
        self.camera = camera 
        self.file_id = file_id

        print ('INFO: Found ffmpeg. Processing video!')
        now_utc = datetime.now(timezone('UTC'))
        now_local = now_utc.astimezone(timezone('America/Phoenix'))
        time_now = now_local.strftime('%Y-%m-%d-%H-%M-%S')
        date_today = now_local.strftime('%Y-%m-%d')
        video_today_path = os.path.join(video_path, date_today)
        motion_today_path = os.path.join(motion_path, date_today)
        if not os.path.exists(video_today_path):
            os.makedirs(video_today_path)
        target_video_path = os.path.join(video_today_path, time_now + '.mp4')
        
        # Add timestamp to images

        #for buffer_entry in camera_buffer[camera]:
            #stamping_target = os.path.join(camera_path, buffer_entry + '.jpg')
            #print ('INFO: Stamping ' + stamping_target)
            #regex = r"(.*/)(.*)\.jpg"

        regex = r"file '(.*/)(.*)\.jpg"
        print ('opening concat_file_name: ' + concat_file_name)
        f = open(concat_file_name, 'r')
        file_contents = f.read()
        f.close()
        with open(concat_file_name, 'r') as input_file:
            # input_file = open(concat_file_name, 'r')
            # print (file_contents)
            for line in input_file:
                try:
                    matches = re.match(regex, line, re.I)
                    path = matches.group(1)
                    filename = matches.group(2)
                    file_utc = datetime.utcfromtimestamp(int(filename)/1000000)
                    file_utc = file_utc.replace(tzinfo=pytz.UTC)
                    file_local = file_utc.astimezone(timezone('America/Phoenix'))
                    image_time = file_local.strftime('%Y-%m-%d %H:%M:%S')
                    input_image_path = camera_path + '/' + filename + '.jpg'
                    print ('Stamping ' + input_image_path + ' with ' + image_time)
                    tmpfile = camera_path + '/_' + filename + '.jpg'

                    os.rename(input_image_path, tmpfile)
                    photo = Image.open(tmpfile)

                    # make the image editable
                    drawing = ImageDraw.Draw(photo)
                    day_color = (3, 8, 12)
                    night_color = (236, 236, 236)
                    color = day_color
                    a = Astral()
                    city = a['Phoenix']
                    now = datetime.now(pytz.utc)
                    sun = city.sun(date=now, local=True)
                    if now >= sun['dusk'] or now <= sun['dawn']:
                            color = night_color
                    else:
                            color = day_color

                    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf", 40)
                    pos = (0, 0)
                    drawing.text(pos, image_time, fill=color, font=font)
                    photo.show()
                    photo.save(input_image_path)
                    os.remove(tmpfile)
                except:
                    print ('Error stamping file ' + input_image_path)

            input_file.close()
            print ('Stamping complete')
            process = Popen([ffmpeg_path, '-r', str(config.frame_rate), '-f', 'concat', '-safe', '0', '-i', concat_file_name, '-vcodec', 'libx264', '-crf', '25', '-pix_fmt', 'yuv420p', target_video_path], stdout=PIPE, stderr=PIPE)
            process.communicate()
            # If the user specified the need to remove images post-processing
            # then clear the image folder from images in the buffer.
            if config.clear_images:
                with open(concat_file_name, 'r') as input_file:
                    for line in input_file:
                        try:
                            matches = re.match(regex, line, re.I)
                            path = matches.group(1)
                            filename = matches.group(2)
                            file_utc = datetime.utcfromtimestamp(int(filename)/1000000)
                            file_utc = file_utc.replace(tzinfo=pytz.UTC)
                            file_local = file_utc.astimezone(timezone('America/Phoenix'))
                            image_time = file_local.strftime('%Y-%m-%d %H:%M:%S')
                            input_image_path = camera_path + '/' + filename + '.jpg'
                            print ('Deleting ' + input_image_path)
                            os.remove(input_image_path)
                        except:
                            print ('Error deleting images')    
            
            print ('Deleting ' + concat_file_name)
            os.remove(concat_file_name)
            print ('INFO: Video processing is complete!')

            # Upload the video
            storage_provider = AzureStorageProvider()

            if bool(config.upload_to_azure):
                print ('INFO: Uploading to Azure Storage...')
                target_blob = 'foggycam/' + camera + '/' + file_id + '.mp4'
                storage_provider.upload_video(account_name=config.az_account_name, sas_token=config.az_sas_token, container='foggycam', blob=target_blob, path=target_video_path)
                print ('INFO: Upload complete.')

            # # If the user specified the need to remove images post-processing
            # # then clear the image folder from images in the buffer.
            # if config.clear_images:
            #     for buffer_entry in camera_buffer[camera]:
            #         deletion_target = os.path.join(camera_path, buffer_entry + '.jpg')
            #         print ('INFO: Deleting ' + deletion_target)
            #         os.remove(deletion_target)
                    
            # Scan for motion
            print ('INFO: Scanning for motion')
            if not os.path.exists(motion_today_path):
                os.makedirs(motion_today_path)
            target_motion_path = os.path.join(motion_today_path, time_now + '.avi')
            a = Astral()
            city = a['Phoenix']
            now = datetime.now(pytz.utc)
            sun = city.sun(date=now, local=True)
            if now >= sun['dusk'] or now <= sun['dawn']:
                process = Popen(['dvr-scan', '-i', target_video_path, '-o', target_motion_path, '-t', '0.5', '-l', '4', '-c', 'h264'])
                process.communicate()
            else:
                process = Popen(['dvr-scan', '-i', target_video_path, '-o', target_motion_path, '-t', '0.5', '-l', '4', '-c', 'h264'])
                process.communicate()
            motion_file_size = os.path.getsize(target_motion_path)
            if motion_file_size == 5686:
                os.remove(target_motion_path)
            print ('INFO: Scanning for motion complete')
