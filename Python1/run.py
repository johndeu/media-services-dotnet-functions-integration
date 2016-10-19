import sys,os
import json


request = open(os.environ['req'], 'r')

output = open(os.environ['res'], 'w')
output.write('Hello World from Python')

