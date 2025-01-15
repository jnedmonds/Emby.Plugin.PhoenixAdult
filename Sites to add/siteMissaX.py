import PAsearchSites
import PAutils


def search(results, lang, siteNum, searchData):
    req = PAutils.HTTPRequest(PAsearchSites.getSearchSearchURL(siteNum) + searchData.encoded)
    searchResults = HTML.ElementFromString(req.text)
    for searchResult in searchResults.xpath('//div[@class="updateItem"] | //div[@class="photo-thumb video-thumb"] | //div[@class="update_details"]'):
        titleNoFormatting = PAutils.parseTitle(searchResult.xpath('.//h4//a | .//p[@class="thumb-title"] | ./a[./preceding-sibling::a]')[0].text_content().strip(), siteNum)
        curID = PAutils.Encode(searchResult.xpath('.//a/@href')[0])

        date = searchResult.xpath('.//span[@class="update_thumb_date"] | .//span[@class="date"] | .//div[contains(@class, "updateDetails")]/p/span[2] | .//div[contains(@class, "update_date")]')
        if date:
            releaseDate = parse(date[0].text_content().strip()).strftime('%Y-%m-%d')
        else:
            releaseDate = searchData.dateFormat() if searchData.date else ''
        displayDate = releaseDate if date else ''

        actors = searchResult.xpath('.//span[contains(@class, "update_models")]//a | .//p[@class="model-name"]//a')
        if actors:
            firstActor = actors[0].text_content().strip()
            numActors = len(actors) - 1

        displayName = '%s [%s] %s' % (titleNoFormatting, PAsearchSites.getSearchSiteName(siteNum), displayDate)
        if firstActor:
            displayName = '%s + %d in %s' % (firstActor, numActors, displayName)

        if searchData.date:
            score = 100 - Util.LevenshteinDistance(searchData.date, releaseDate)
        else:
            score = 100 - Util.LevenshteinDistance(searchData.title.lower(), titleNoFormatting.lower())

        results.Append(MetadataSearchResult(id='%s|%d|%s' % (curID, siteNum, releaseDate), name=displayName, score=score, lang=lang))

    return results


def update(metadata, lang, siteNum, movieGenres, movieActors, art):
    metadata_id = str(metadata.id).split('|')
    sceneURL = PAutils.Decode(metadata_id[0])
    if not sceneURL.startswith('http'):
        sceneURL = PAsearchSites.getSearchBaseURL(siteNum) + sceneURL
    if len(metadata_id) > 2:
        sceneDate = metadata_id[2]
    req = PAutils.HTTPRequest(sceneURL)
    detailsPageElements = HTML.ElementFromString(req.text)

    # Title
    metadata.title = PAutils.parseTitle(detailsPageElements.xpath('//span[@class="update_title"] | //p[@class="raiting-section__title"]')[0].text_content().strip(), siteNum)

    # Summary
    summary = detailsPageElements.xpath('//span[@class="latest_update_description"] | //p[contains(@class, "text")]')
    if summary:
        metadata.summary = summary[0].text_content().replace('Includes:', '').replace('Synopsis:', '').strip()

    # Studio
    metadata.studio = PAsearchSites.getSearchSiteName(siteNum)

    # Tagline and Collection(s)
    tagline = metadata.studio
    metadata.tagline = tagline
    metadata.collections.add(tagline)

    # Release Date
    updateDate = detailsPageElements.xpath('//span[@class="update_date"] | //span[contains(@class, "availdate")]')
    dvdSceneDate = detailsPageElements.xpath('//p[@class="dvd-scenes__data"]')
    if updateDate:
        date_object = parse(updateDate[0].text_content().replace('Available to Members Now', '').strip())
    elif dvdSceneDate:
        date_object = parse(dvdSceneDate[0].text_content().split('|')[1].replace('Added:', '').strip())
    elif sceneDate:
        date_object = parse(sceneDate)
    if updateDate or dvdSceneDate or sceneDate:
        metadata.originally_available_at = date_object
        metadata.year = metadata.originally_available_at.year

    # Actor(s)
    actors = detailsPageElements.xpath('//div[contains(@class, "update_block")]/span[@class="tour_update_models"]//a | //p[@class="dvd-scenes__data"][1]//a')
    for actorLink in actors:
        actorName = actorLink.text_content().strip()
        actorPhotoURL = ''

        if siteNum == 1264 and metadata.title.endswith(': ' + actorName):
            metadata.title = metadata.title[:-len(': ' + actorName)]

        actorPageURL = actorLink.get('href')
        req = PAutils.HTTPRequest(actorPageURL)
        actorPageElements = HTML.ElementFromString(req.text)
        actorPhotoElement = actorPageElements.xpath('//img[contains(@class, "model_bio_thumb")]/@src0_1x')
        if actorPhotoElement:
            actorPhotoURL = actorPhotoElement[0]
            if not actorPhotoURL.startswith('http'):
                actorPhotoURL = PAsearchSites.getSearchBaseURL(siteNum) + actorPhotoURL

        movieActors.addActor(actorName, actorPhotoURL)

    # Genres
    genres = detailsPageElements.xpath('//span[contains(@class, "update_tags")]//a | //p[@class="dvd-scenes__data"][2]//a')
    for genreLink in genres:
        genreName = genreLink.text_content()

        movieGenres.addGenre(genreName)

    # Posters/Background
    xpaths = [
        '//img[contains(@class, "update_thumb")]/@src0_4x',
        '//img[contains(@class, "update_thumb")]/@src0_1x',
    ]

    for xpath in xpaths:
        for img in detailsPageElements.xpath(xpath):
            if not img.startswith('http'):
                img = PAsearchSites.getSearchBaseURL(siteNum) + '/' + img
            art.append(img)

    Log('Artwork found: %d' % len(art))
    images = []
    postersClean = list()
    posterExists = False
    for idx, posterUrl in enumerate(art, 1):
        # Remove Timestamp and Token from URL
        cleanUrl = posterUrl.split('?')[0]
        postersClean.append(cleanUrl)
        if not PAsearchSites.posterAlreadyExists(cleanUrl, metadata):
            # Download image file for analysis
            try:
                image = PAutils.HTTPRequest(posterUrl)
                im = StringIO(image.content)
                resized_image = Image.open(im)
                width, height = resized_image.size
                # Add the image proxy items to the collection
                if height > width:
                    # Item is a poster
                    metadata.posters[cleanUrl] = Proxy.Media(image.content, sort_order=idx)
                    posterExists = True
                if width > height:
                    # Item is an art item
                    images.append((image, cleanUrl))
                    metadata.art[cleanUrl] = Proxy.Media(image.content, sort_order=idx)
            except:
                pass
        elif PAsearchSites.posterOnlyAlreadyExists(cleanUrl, metadata):
            posterExists = True

    if not posterExists:
        for idx, (image, cleanUrl) in enumerate(images, 1):
            try:
                im = StringIO(image.content)
                resized_image = Image.open(im)
                width, height = resized_image.size
                # Add the image proxy items to the collection
                if width > 1:
                    # Item is a poster
                    metadata.posters[cleanUrl] = Proxy.Media(image.content, sort_order=idx)
            except:
                pass

    art.extend(postersClean)

    return metadata
