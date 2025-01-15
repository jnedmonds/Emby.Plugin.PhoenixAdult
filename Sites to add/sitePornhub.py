import PAsearchSites
import PAutils


def search(results, lang, siteNum, searchData):
    Log('---- sitePornhub-search(): starting with title: %s' % searchData.title)

    req = PAutils.HTTPRequest(PAsearchSites.getSearchSearchURL(siteNum) + searchData.encoded)
    searchResults = HTML.ElementFromString(req.text)
    Log('---- sitePornhub-search(): seaching')

    for searchResult in searchResults.xpath('//ul[@id="videoSearchResult"]/li[@data-video-vkey]/div/div[3]/span/a'):

        titleNoFormatting = searchResult.get('title')
        Log('---- sitePornhub-search(): found result title       - %s' % titleNoFormatting)
        
        curID = PAutils.Encode(searchResult.get('href'))
        score = 100 - Util.LevenshteinDistance(searchData.title.lower(), titleNoFormatting.lower())

        results.Append(MetadataSearchResult(id='%s|%d' % (curID, siteNum), name='%s [Pornhub]' % titleNoFormatting, score=score, lang=lang))

    Log('---- sitePornhub-search(): leaving')

    return results


def update(metadata, lang, siteNum, movieGenres, movieActors, art):
    Log('---- sitePornhub-update(): starting')
    
    metadata_id = str(metadata.id).split('|')
    sceneURL = PAutils.Decode(metadata_id[0])
    if not sceneURL.startswith('http'):
        sceneURL = PAsearchSites.getSearchBaseURL(siteNum) + sceneURL

    Log('---- sitePornhub-update(): sceneURL: %s' % sceneURL)
    
    req = PAutils.HTTPRequest(sceneURL)
    detailsPageElements = HTML.ElementFromString(req.text)

    # Title
    metadata.title = detailsPageElements.xpath('//h1[@class="title"]')[0].text_content().strip()

    Log('---- sitePornhub-update(): updating - title         - %s' % metadata.title)

    # Summary
    try:
        metadata.summary = ''
    except:
        pass

    # Studio
    metadata.studio = 'Pornhub'

    # Tagline and Collection(s)
    metadata.collections.add(metadata.studio)

    tagline = detailsPageElements.xpath('//div[@class="userInfo"]//a')[0].text_content().strip()
    Log('---- sitePornhub-update(): studio tagline: %s' % tagline)
    
    metadata.tagline = tagline
    metadata.collections.add(tagline)
    
    # Release Date
    # date = detailsPageElements.xpath('//ul[@class="more-info"]//li[2]')[0].text_content().replace('RELEASE DATE:', '').strip()
    # date_object = parse(date)
    # metadata.originally_available_at = date_object
    # metadata.year = metadata.originally_available_at.year

    # Genres
    for genreLink in detailsPageElements.xpath('(//div[@class="categoriesWrapper"] | //div[@class="tagsWrapper"])/a'):
        genreName = genreLink.text_content().title()

        Log('---- sitePornhub-update(): genres: %s' % genreName)

        movieGenres.addGenre(genreName)

    # Actor(s)
    for actorLink in detailsPageElements.xpath('//div[contains(@class, "pornstarsWrapper")]/a'):
        actorName = actorLink.text_content().title().strip()
        actorPhotoURL = actorLink.xpath("//img[@class='avatar']/@src")
        
        Log('---- sitePornhub-update(): actor name:    %s' % actorName)        
        Log('----                     : actor picture: %s' % actorPhotoURL)
       
        movieActors.addActor(actorName, actorPhotoURL)

    # # Posters/Background
    # try:
        # background =  .xpath('//div[@class="fakeplayer"]//img/@src0_1x')[0]
    # except:
        # background = detailsPageElements.xpath('//div[@class="fakeplayer"]//img/@src0_1x')[0]

    # art.append(background)

    # Log('Artwork found: %d' % len(art))
    # for idx, posterUrl in enumerate(art, 1):
        # if not PAsearchSites.posterAlreadyExists(posterUrl, metadata):
            # # Download image file for analysis
            # try:
                # image = PAutils.HTTPRequest(posterUrl)
                # im = StringIO(image.content)
                # resized_image = Image.open(im)
                # width, height = resized_image.size
                # # Add the image proxy items to the collection
                # if width > 1:
                    # # Item is a poster
                    # metadata.posters[posterUrl] = Proxy.Media(image.content, sort_order=idx)
                # if width > 100:
                    # # Item is an art item
                    # metadata.art[posterUrl] = Proxy.Media(image.content, sort_order=idx)
            # except:
                # pass

    Log('---- sitePornhub-update(): leaving')

    return metadata
