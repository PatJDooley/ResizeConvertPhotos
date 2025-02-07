# ResizeConvertPhotos
C# XAML Program for Resizing and Converting Images by folder
This program combines two capabilities that photographers may find useful.

1. It resizes photos in bulk.

I Photograph sporting events such as marathons. At the of the day I may have 1000 photos to post on social media or a wessite. 
Usually there are size limitations on how large your image files can be for display on the target platform. This program lets
you resize all the files in one or more folders to satisy a size limitation. It is best for making photos smaller. If you need
to significantly inrease the size of a photograph, then this is probably not a good choice. The program has the capaility to add
the file name of the photograph to the photograph along with an optional copyright message. The idea here is that a viewer can 
request a full-sized version of the photo by citing the image name.

2. It converts file types in bulk.

I have a lot of files on my harddrive. They take up 700GB even though they are almost all JPG files. I discovered that the
WebP format is much more space efficient and I could reduce my repository of photos to around 250GB just by converting them
to webP. But how to do it? I found the ImageMagick library that can read image files in a variety of formats, resize them if wanted, and 
save them in a different format. Specifically, it could read JPG files and write WebP files. I chose to support formats
that photographers regularly encounter. 

            cboFileType.Items.Add("bmp");
            cboFileType.Items.Add("gif");
            cboFileType.Items.Add("jpeg");
            cboFileType.Items.Add("jpg");
            cboFileType.Items.Add("png");
            cboFileType.Items.Add("tiff");
            cboFileType.Items.Add("webp");

More could be added provided ImageMagick supports them.

The program is written in C# and is a Windows WFP project. It is the first app I've developed using XAML I was used to
Windows Forms and HTML/CSS so there was a little learning curve to figure out how to design a form using XAML.
