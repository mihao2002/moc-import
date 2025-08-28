#ifdef __APPLE__  // Only compile for iOS/macOS
#import <UIKit/UIKit.h>

extern "C" {
    void _ShowShareSheet(const char* filePath)
    {
        NSString* path = [NSString stringWithUTF8String:filePath];
        NSURL* fileURL = [NSURL fileURLWithPath:path];

        UIActivityViewController* activityVC = [[UIActivityViewController alloc] initWithActivityItems:@[fileURL] applicationActivities:nil];

        UIViewController* rootVC = [UIApplication sharedApplication].keyWindow.rootViewController;
        [rootVC presentViewController:activityVC animated:YES completion:nil];
    }
}
#endif
