# -*- coding: utf-8 -*-
"""PyInstaller 入口：桌面开关"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from switch.main import main

if __name__ == "__main__":
    main()
