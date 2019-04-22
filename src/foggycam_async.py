"""FoggyCam captures Nest camera images and generates a video."""

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
from queue import Queue
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

class FoggyCam(object):
    """FoggyCam client class that performs capture operations."""

    nest_username = ''
    nest_password = ''

    nest_user_id = ''
    nest_access_token = ''
    nest_access_token_expiration = ''
    nest_current_user = None

    nest_session_url = 'https://home.nest.com/session'
    nest_user_url = 'https://home.nest.com/api/0.1/user/#USERID#/app_launch'
    nest_api_login_url = 'https://webapi.camera.home.nest.com/api/v1/login.login_nest'
    nest_image_url = 'https://nexusapi-us1.camera.home.nest.com/get_image?uuid=#CAMERAID#&width=#WIDTH#&cachebuster=#CBUSTER#'
    nest_verify_pin_url = 'https://home.nest.com/api/0.1/2fa/verify_pin'

    nest_user_request_payload = {
        "known_bucket_types":["quartz"],
        "known_bucket_versions":[]
    }

    nest_camera_array = []
    nest_camera_buffer_threshold = 50

    is_capturing = False
    cookie_jar = None
    merlin = None
    temp_dir_path = ''
    local_path = ''
    image_list_queue = Queue()

    def __init__(self, username, password):
        self.nest_password = password
        self.nest_username = username
        self.cookie_jar = CookieJar()
        self.merlin = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cookie_jar))

        if not os.path.exists('_temp'):
            os.makedirs('_temp')

        self.local_path = os.path.dirname(os.path.abspath(__file__))
        self.temp_dir_path = os.path.join(self.local_path, '_temp')

        # It's important to try and load the cookies first to check
        # if we can avoid logging in.
        try:
            self.unpickle_cookies()
            
            utc_date = datetime.utcnow()
            utc_millis_str = str(int(utc_date.timestamp())*1000)
            self.initialize_twof_session(utc_millis_str)
        except:
            print ("Failed to re-use the cookies. Re-initializing session...")
            self.initialize_session()
        
        self.login()
        self.initialize_user()
    
    def unpickle_cookies(self):
        """Get local cookies and load them into the cookie jar."""

        print ("Unpickling cookies...")
        with open("cookies.bin", 'rb') as f:
            pickled_cookies = pickle.load(f)

            for pickled_cookie in pickled_cookies:
                self.cookie_jar.set_cookie(pickled_cookie)

            cookie_data = dict((cookie.name, cookie.value) for cookie in self.cookie_jar)

            self.nest_access_token = cookie_data["cztoken"]

    def pickle_cookies(self):
        """Store the cookies locally to reduce auth calls."""

        print ("Pickling cookies...")
        pickle.dump([c for c in self.cookie_jar], open("cookies.bin", "wb"))

    def initialize_twof_session(self, time_token):
        """Creates the first session to get the access token and cookie, with 2FA enabled."""

        print ("Intializing 2FA session...")

        target_url = self.nest_session_url + "?=_" + time_token
        print (target_url)

        try:
            request = urllib.request.Request(target_url)
            request.add_header('Authorization', 'Basic %s' % self.nest_access_token)

            response = self.merlin.open(request)
            session_data = response.read().decode('utf-8')

            session_json = json.loads(session_data)

            self.nest_access_token = session_json['access_token']
            self.nest_access_token_expiration = session_json['expires_in']
            self.nest_user_id = session_json['userid']

            self.pickle_cookies()
        except urllib.request.HTTPError as err:
            print (err)

    def initialize_session(self):
        """Creates the first session to get the access token and cookie."""

        print ('INFO: Initializing session...')

        payload = {'email':self.nest_username, 'password':self.nest_password}
        binary_data = json.dumps(payload).encode('utf-8')

        request = urllib.request.Request(self.nest_session_url, binary_data)
        request.add_header('Content-Type', 'application/json')

        try:
            response = self.merlin.open(request)
            session_data = response.read().decode('utf-8')
            session_json = json.loads(session_data)

            self.nest_access_token = session_json['access_token']
            self.nest_access_token_expiration = session_json['expires_in']
            self.nest_user_id = session_json['userid']

            print ('INFO: [PARSED] Captured authentication token:')
            print (self.nest_access_token)

            print ('INFO: [PARSED] Captured expiration date for token:')
            print (self.nest_access_token_expiration)

            cookie_data = dict((cookie.name, cookie.value) for cookie in self.cookie_jar)
            for cookie in cookie_data:
                print (cookie)

            print ('INFO: [COOKIE] Captured authentication token:')
            print (cookie_data["cztoken"])
        except urllib.request.HTTPError as err:
            if err.code == 401:
                error_message = err.read().decode('utf-8')
                unauth_content = json.loads(error_message)

                if unauth_content["status"].lower() == "verification_pending":
                    print ("Pending 2FA verification!")

                    two_factor_token = unauth_content["2fa_token"]
                    phone_truncated = unauth_content["truncated_phone_number"]

                    print ("Enter PIN you just received on number ending with", phone_truncated)
                    pin = input()

                    payload = {"pin":pin ,"2fa_token":two_factor_token}
                    binary_data = json.dumps(payload).encode('utf-8')

                    request = urllib.request.Request(self.nest_verify_pin_url, binary_data)
                    request.add_header('Content-Type', 'application/json')

                    try:
                        response = self.merlin.open(request)
                        pin_attempt = response.read().decode('utf-8')

                        parsed_pin_attempt = json.loads(pin_attempt)
                        if parsed_pin_attempt["status"].lower() == "id_match_positive":
                            print ("2FA verification successful.")

                            utc_date = datetime.utcnow()
                            utc_millis_str = str(int(utc_date.timestamp())*1000)
                            
                            print ("Targetting new session with timestamp: ", utc_millis_str)
                            
                            cookie_data = dict((cookie.name, cookie.value) for cookie in self.cookie_jar)

                            print ('INFO: [COOKIE] Captured authentication token:')
                            print (cookie_data["cztoken"])

                            self.nest_access_token = parsed_pin_attempt['access_token']

                            self.initialize_twof_session(utc_millis_str)
                        else:
                            print ("Could not verify. Exiting...")
                            exit()
                    
                    except:
                        traceback.print_exc()

                        print ("Failed 2FA checks. Exiting...")
                        exit()

        print ('INFO: Session initialization complete!')

    def login(self):
        """Performs user login to get the website_2 cookie."""

        print ('INFO: Performing user login...')

        post_data = {'access_token':self.nest_access_token}
        post_data = urllib.parse.urlencode(post_data)
        binary_data = post_data.encode('utf-8')

        print ("INFO: Auth post data")
        print (post_data)

        request = urllib.request.Request(self.nest_api_login_url, data=binary_data)
        request.add_header('Content-Type', 'application/x-www-form-urlencoded')

        response = self.merlin.open(request)
        session_data = response.read().decode('utf-8')

        print (session_data)

    def initialize_user(self):
        """Gets the assets belonging to Nest user."""

        print ('INFO: Initializing current user...')

        user_url = self.nest_user_url.replace('#USERID#', self.nest_user_id)

        print ('INFO: Requesting user data from:')
        print (user_url)

        binary_data = json.dumps(self.nest_user_request_payload).encode('utf-8')

        request = urllib.request.Request(user_url, binary_data)

        request.add_header('Content-Type', 'application/json')
        request.add_header('Authorization', 'Basic %s' % self.nest_access_token)

        response = self.merlin.open(request)

        response_data = response.read().decode('utf-8')

        print (response_data)

        user_object = json.loads(response_data)
        for bucket in user_object['updated_buckets']:
            bucket_id = bucket['object_key']
            if bucket_id.startswith('quartz.'):
                camera_id = bucket_id.replace('quartz.', '')
                print ('INFO: Detected camera configuration.')
                print (bucket)
                print ('INFO: Camera UUID:')
                print (camera_id)
                self.nest_camera_array.append(camera_id)

    def capture_images(self, config=None):
        """Starts the multi-threaded image capture process."""

        print ('[', threading.current_thread().name, '] INFO: Capturing images...')

        self.is_capturing = True

        if not os.path.exists('capture'):
            os.makedirs('capture')

        self.nest_camera_buffer_threshold = config.threshold

        for camera in self.nest_camera_array:
            camera_path = ''
            video_path = ''
            motion_path = ''

            # Determine whether the entries should be copied to a custom path
            # or not.
            if not config.path:
                camera_path = os.path.join(self.local_path, 'capture', camera, 'images')
                video_path = os.path.join(self.local_path, 'capture', camera, 'video')
                motion_path = os.path.join(self.local_path, 'capture', camera, 'motion')
            else:
                camera_path = os.path.join(config.path, 'capture', camera, 'images')
                video_path = os.path.join(config.path, 'capture', camera, 'video')
                motion_path = os.path.join(config.path, 'capture', camera, 'motion')

            # Provision the necessary folders for images and videos.
            if not os.path.exists(camera_path):
                os.makedirs(camera_path)

            if not os.path.exists(video_path):
                os.makedirs(video_path)

            if not os.path.exists(motion_path):
                os.makedirs(motion_path)

            t1 = threading.Thread(target=self.perform_capture, args=(config, camera, camera_path, video_path, motion_path))
            t1.start()

            # Check if we have ffmpeg locally
            use_terminal = False
            ffmpeg_path = ''

            if shutil.which("ffmpeg"):
                ffmpeg_path = 'ffmpeg'
                use_terminal = True
            else:
                ffmpeg_path = os.path.abspath(os.path.join(os.path.dirname( __file__ ), '..', 'tools', 'ffmpeg'))
            
            if use_terminal or (os.path.isfile(ffmpeg_path) and use_terminal is False):
                # Start async processing
                print ('[', threading.current_thread().name, '] starting processing thread')
                t2 = threading.Thread(target=self.process_images, args=(config, video_path, motion_path, camera_path, ffmpeg_path, camera))
                t2.start()
                t2.join()
                print ('[', threading.current_thread().name, '] t2 has returned')
                # process_thread = threading.Thread(target=ProcessImages, args=(config, camera_buffer, video_path, motion_path, concat_file_name, camera_path, ffmpeg_path, camera, file_id))
                # process_thread.start()
                # process_thread.join()
                #end async processing
            else:
                print ('[', threading.current_thread().name, '] WARNING: No ffmpeg detected. Make sure the binary is in /tools.')
            
        while True:
            time.sleep(0.5)

    def perform_capture(self, config=None, camera=None, camera_path='', video_path='', motion_path=''):
        """Captures images and generates the video from them."""

        camera_buffer = defaultdict(list)
        
        while self.is_capturing:
        
            vidstart_utc = datetime.now(timezone('UTC'))
            vidstart_local = vidstart_utc.astimezone(timezone('America/Phoenix'))
            vidstart_now = vidstart_local.strftime('%Y-%m-%d-%H-%M-%S')
            
            # file_id = str(uuid.uuid4().hex)
            now_utc = datetime.now(timezone('UTC'))
            file_id = str(int(now_utc.timestamp()*1000000))

            utc_date = datetime.utcnow()
            utc_millis_str = str(int(utc_date.timestamp())*1000)

            # print ('Applied cache buster: ', utc_millis_str)

            image_url = self.nest_image_url.replace('#CAMERAID#', camera).replace('#CBUSTER#', utc_millis_str).replace('#WIDTH#', str(config.width))
            # print ('Processing next image: ' + image_url)
            request = urllib.request.Request(image_url)
            request.add_header('accept', 'image/webp,image/apng,image/*,*/*;q=0.9')
            request.add_header('accept-encoding', 'gzip, deflate, br')
            request.add_header('user-agent', 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_13_4) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/66.0.3359.181 Safari/537.36')
            request.add_header('referer','https://home.nest.com/')
            request.add_header('authority','nexusapi-us1.camera.home.nest.com')

            try:
                response = self.merlin.open(request)
                time.sleep(0.5)
                with open(camera_path + '/' + file_id + '.jpg', 'wb') as image_file:
                    for chunk in response:
                        image_file.write(chunk)

                # Check if we need to compile a video
                if config.produce_video:
                    camera_buffer_size = len(camera_buffer[camera])
                    print ('[', threading.current_thread().name, '] INFO: Camera buffer size for ', camera, ': ', camera_buffer_size)

                    if camera_buffer_size < self.nest_camera_buffer_threshold:
                        camera_buffer[camera].append(file_id)
                    else:
                        camera_image_folder = os.path.join(self.local_path, camera_path)

                        # Build the batch of files that need to be made into a video.
                        file_declaration = ''
                        for buffer_entry in camera_buffer[camera]:
                            file_declaration = file_declaration + 'file \'' + camera_image_folder + '/' + buffer_entry + '.jpg\'\n'
                        concat_file_name = os.path.join(self.temp_dir_path, vidstart_now + '.txt')

                        # Make sure that the content is decoded

                        with open(concat_file_name, 'w') as declaration_file:
                            declaration_file.write(file_declaration)

                        print ('[', threading.current_thread().name, '] Creating ', concat_file_name)
                        self.image_list_queue.put(concat_file_name)

                        # Empty buffer, since we no longer need the file records that we're planning
                        # to compile in a video.
                        print ('[', threading.current_thread().name, '] Clearing camera buffer')
                        camera_buffer[camera] = []

            except urllib.request.HTTPError as err:
                if err.code == 403:
                    self.initialize_session()
                    self.login()
                    self.initialize_user()
            except Exception:
                print ('[', threading.current_thread().name, '] ERROR: Could not download image from URL:')
                print ('[', threading.current_thread().name, '] ', image_url)

                traceback.print_exc()

    def process_images(self, config, video_path, motion_path, camera_path, ffmpeg_path, camera):

        self.config = config
        # self.camera_buffer = camera_buffer
        self.video_path = video_path
        self.motion_path = motion_path
        # self.concat_file_name = concat_file_name
        self.camera_path = camera_path
        self.ffmpeg_path = ffmpeg_path
        self.camera = camera 
        # self.file_id = file_id

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
        while True:
            print ('[', threading.current_thread().name, '] Starting processing loop')
            concat_file_name = self.image_list_queue.get()
            if concat_file_name:
                print ('[', threading.current_thread().name, '] Beging processing ', concat_file_name)
                regex = r"file '(.*/)(.*)\.jpg"
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
                            print ('[', threading.current_thread().name, '] Stamping ' + filename + '.jpg with ' + image_time)
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
                            print ('[', threading.current_thread().name, '] Error stamping file ' + input_image_path)

                    input_file.close()

                    now_utc = datetime.now(timezone('UTC'))
                    now_local = now_utc.astimezone(timezone('America/Phoenix'))
                    time_now = now_local.strftime('%Y-%m-%d-%H-%M-%S')
                    date_today = now_local.strftime('%Y-%m-%d')
                    video_today_path = os.path.join(video_path, date_today)
                    motion_today_path = os.path.join(motion_path, date_today)
                    if not os.path.exists(video_today_path):
                        os.makedirs(video_today_path)
                    target_video_path = os.path.join(video_today_path, time_now + '.mp4')

                    print ('[', threading.current_thread().name, '] Stamping complete')
                    print ('[', threading.current_thread().name, '] Starting ffmpeg: ', target_video_path)
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
                                    print ('[', threading.current_thread().name, '] Deleting ' + input_image_path)
                                    os.remove(input_image_path)
                                except:
                                    print ('[', threading.current_thread().name, '] Error deleting images')    
                    
                    print ('[', threading.current_thread().name, '] Deleting ' + concat_file_name)
                    os.remove(concat_file_name)
                    print ('[', threading.current_thread().name, '] INFO: Video processing is complete!')

                    # Upload the video
                    storage_provider = AzureStorageProvider()

                    # if bool(config.upload_to_azure):
                    #     print ('INFO: Uploading to Azure Storage...')
                    #     target_blob = 'foggycam/' + camera + '/' + file_id + '.mp4'
                    #     storage_provider.upload_video(account_name=config.az_account_name, sas_token=config.az_sas_token, container='foggycam', blob=target_blob, path=target_video_path)
                    #     print ('INFO: Upload complete.')

                    # # If the user specified the need to remove images post-processing
                    # # then clear the image folder from images in the buffer.
                    # if config.clear_images:
                    #     for buffer_entry in camera_buffer[camera]:
                    #         deletion_target = os.path.join(camera_path, buffer_entry + '.jpg')
                    #         print ('INFO: Deleting ' + deletion_target)
                    #         os.remove(deletion_target)
                            
                    # Scan for motion
                    print ('[', threading.current_thread().name, '] INFO: Scanning for motion')
                    if not os.path.exists(motion_today_path):
                        os.makedirs(motion_today_path)
                    target_motion_path = os.path.join(motion_today_path, time_now + '.avi')
                    a = Astral()
                    city = a['Phoenix']
                    now = datetime.now(pytz.utc)
                    sun = city.sun(date=now, local=True)
                    print ('[', threading.current_thread().name, '] INFO: Input: target_video_path', target_video_path)
                    print ('[', threading.current_thread().name, '] INFO: Output: target_video_path', target_motion_path)
                    if os.path.exists(target_video_path):
                        if now >= sun['dusk'] or now <= sun['dawn']:
                            process = Popen(['dvr-scan', '-i', target_video_path, '-o', target_motion_path, '-t', '0.5', '-l', '4', '-c', 'h264'])
                            process.communicate()
                        else:
                            process = Popen(['dvr-scan', '-i', target_video_path, '-o', target_motion_path, '-t', '0.5', '-l', '4', '-c', 'h264'])
                            process.communicate()
                        motion_file_size = os.path.getsize(target_motion_path)
                        if motion_file_size == 5686:
                            os.remove(target_motion_path)
                        print ('[', threading.current_thread().name, '] INFO: Scanning for motion complete')
                    else:
                        print ('[', threading.current_thread().name, '] ERROR: Input file doesn\'t exist')
                # self.image_list_queue.task_done()
            else:
                print ('[', threading.current_thread().name, '] file list queue empty')
                # self.image_list_queue.task_done()
